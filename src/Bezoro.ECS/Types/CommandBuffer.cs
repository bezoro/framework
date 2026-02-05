using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
///     Records structural changes to be applied after system execution.
/// </summary>
public sealed class CommandBuffer
{
	private readonly List<Command> _commands = [];
	private readonly object        _sync     = new();
	private readonly World         _world;
	private          int           _nextTemporaryId = -1;

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
			var entity = new Entity(_nextTemporaryId--, 0);
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
	///     Applies all recorded commands to the world and clears the buffer.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when a temporary entity has not been created in this buffer.</exception>
	public void Playback()
	{
		Command[] commands;
		lock (_sync)
		{
			if (_commands.Count == 0) return;

			commands = new Command[_commands.Count];
			_commands.CopyTo(commands);
			_commands.Clear();
		}

		var tempEntities = new Dictionary<int, Entity>();

		for (var i = 0; i < commands.Length; i++)
		{
			var command = commands[i];
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
					_world.RemoveComponentById(ResolveEntity(command.Entity, tempEntities), command.ComponentTypeId);
					break;
				default:
					throw new ArgumentOutOfRangeException();
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
}
