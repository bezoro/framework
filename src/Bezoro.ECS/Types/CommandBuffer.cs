using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
///     Records structural changes to be applied after system execution.
/// </summary>
public sealed class CommandBuffer
{
	private readonly List<Command>            _commands                  = [];
	private readonly Dictionary<int, Entity> _resolvedTemporaryEntities = [];
	private readonly object                   _sync                     = new();
	private readonly World                    _world;
	private          bool                     _isPlayingBack;
	private          int                      _nextTemporaryId = -1;

	internal CommandBuffer(World world)
	{
		_world = world;
	}

	internal bool HasCommands
	{
		get
		{
			lock (_sync)
			{
				return _commands.Count > 0;
			}
		}
	}

	/// <summary>
	///     Creates a new entity that will be added to the empty archetype on playback.
	/// </summary>
	/// <returns>The reserved entity handle.</returns>
	public Entity CreateEntity() =>
		CreateEntity(_world.EmptyArchetype);

	/// <summary>
	///     Creates a new entity that will be added to the specified archetype on playback.
	/// </summary>
	/// <param name="archetype">The archetype the entity should belong to.</param>
	/// <returns>The reserved entity handle.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="archetype" /> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the archetype belongs to a different world.</exception>
	public Entity CreateEntity(Archetype archetype)
	{
		if (archetype is null) throw new ArgumentNullException(nameof(archetype));

		_world.EnsureOwnedArchetype(archetype);

		lock (_sync)
		{
			var entity = new Entity(_nextTemporaryId--, 0, _world.WorldId);
			_commands.Add(new Command(CommandType.CreateEntity, entity, archetype, -1, null));
			return entity;
		}
	}

	/// <summary>
	///     Adds a component to an entity when the command buffer is played back.
	/// </summary>
	/// <typeparam name="T">The component type.</typeparam>
	/// <param name="entity">The entity to modify.</param>
	/// <param name="component">The component value to add.</param>
	public void AddComponent<T>(Entity entity, in T component) where T : struct, IComponent
	{
		int typeId     = ComponentTypeRegistry.GetOrCreate<T>();
		var applicator = new ComponentApplicator<T>(typeId, in component, addOnly: true);
		Enqueue(new(CommandType.AddComponent, entity, null, typeId, applicator));
	}

	/// <summary>
	///     Destroys an entity when the command buffer is played back.
	/// </summary>
	/// <param name="entity">The entity to destroy.</param>
	public void DestroyEntity(Entity entity) =>
		Enqueue(new Command(CommandType.DestroyEntity, entity, null, -1, null));

	/// <summary>
	///     Applies all recorded commands to the world.
	/// </summary>
	/// <remarks>
	///     Playback is not allowed while systems are executing inside <see cref="World.Update(float)" />.
	///     If playback fails, successfully processed commands are removed and unprocessed commands stay queued for retry.
	/// </remarks>
	/// <exception cref="InvalidOperationException">Thrown when playback is invoked during world update.</exception>
	/// <exception cref="InvalidOperationException">Thrown when a temporary entity has not been created in this buffer.</exception>
	public void Playback() =>
		PlaybackInternal(allowDuringUpdate: false);

	internal void PlaybackInternal(bool allowDuringUpdate)
	{
		if (_world.HasActiveQueryIteration)
			throw new InvalidOperationException("Playback cannot run during query iteration.");

		if (!allowDuringUpdate && _world.IsUpdating)
			throw new InvalidOperationException("Playback cannot run during world update.");

		Command[] commands;
		Dictionary<int, Entity> tempEntities;
		lock (_sync)
		{
			if (_isPlayingBack)
				throw new InvalidOperationException("CommandBuffer playback is already in progress.");

			if (_commands.Count == 0) return;

			commands = new Command[_commands.Count];
			_commands.CopyTo(commands);
			tempEntities = _resolvedTemporaryEntities.Count == 0 ? [] : new(_resolvedTemporaryEntities);
			_isPlayingBack = true;
		}

		var processedCount = 0;
		try
		{
			for (; processedCount < commands.Length; processedCount++)
			{
				var command = commands[processedCount];
				switch (command.Type)
				{
					case CommandType.CreateEntity:
					{
						var archetype = command.Archetype ?? _world.EmptyArchetype;
						_world.EnsureOwnedArchetype(archetype);
						var entity = _world.CreateEntityInternal(archetype);
						tempEntities[command.Entity.Id] = entity;
						break;
					}
					case CommandType.DestroyEntity:
						_world.DestroyEntityInternal(ResolveEntity(command.Entity, tempEntities));
						break;
					case CommandType.AddComponent:
					case CommandType.SetComponent:
						command.Applicator!.Apply(_world, ResolveEntity(command.Entity, tempEntities));
						break;
					case CommandType.RemoveComponent:
					{
						var entity = ResolveEntity(command.Entity, tempEntities);
						_world.EnsureEntityAlive(entity);
						_world.RemoveComponentById(entity, command.ComponentTypeId);
						break;
					}
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}
		finally
		{
			lock (_sync)
			{
				RemoveProcessedCommandsUnsafe(processedCount);
				ReplaceResolvedTemporaryEntitiesUnsafe(tempEntities);
				_isPlayingBack = false;
			}
		}
	}

	/// <summary>
	///     Removes a component from an entity when the command buffer is played back.
	/// </summary>
	/// <typeparam name="T">The component type.</typeparam>
	/// <param name="entity">The entity to modify.</param>
	public void RemoveComponent<T>(Entity entity) where T : struct, IComponent
	{
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		Enqueue(new Command(CommandType.RemoveComponent, entity, null, typeId, null));
	}

	/// <summary>
	///     Sets a component value when the command buffer is played back.
	/// </summary>
	/// <typeparam name="T">The component type.</typeparam>
	/// <param name="entity">The entity to modify.</param>
	/// <param name="component">The component value to set.</param>
	public void SetComponent<T>(Entity entity, in T component) where T : struct, IComponent
	{
		int typeId     = ComponentTypeRegistry.GetOrCreate<T>();
		var applicator = new ComponentApplicator<T>(typeId, in component, addOnly: false);
		Enqueue(new(CommandType.SetComponent, entity, null, typeId, applicator));
	}

	private static Entity ResolveEntity(Entity entity, Dictionary<int, Entity> tempEntities)
	{
		if (entity.Id >= 0) return entity;

		if (tempEntities.TryGetValue(entity.Id, out var resolved))
			return resolved;

		throw new InvalidOperationException($"Temporary entity {entity.Id} was not created in this command buffer.");
	}

	private void Enqueue(Command command)
	{
		lock (_sync)
		{
			_commands.Add(command);
		}
	}

	private void RemoveProcessedCommandsUnsafe(int processedCount)
	{
		if (processedCount <= 0 || _commands.Count == 0) return;

		if (processedCount > _commands.Count)
			processedCount = _commands.Count;

		_commands.RemoveRange(0, processedCount);
	}

	private void ReplaceResolvedTemporaryEntitiesUnsafe(Dictionary<int, Entity> source)
	{
		_resolvedTemporaryEntities.Clear();
		foreach (var entry in source)
			_resolvedTemporaryEntities[entry.Key] = entry.Value;

		if (_commands.Count == 0)
			_resolvedTemporaryEntities.Clear();
	}
}
