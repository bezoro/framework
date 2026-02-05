using Bezoro.ECS.Internal;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Services;

/// <summary>
///     Manages the lifecycle of entities within the Entity Component System (ECS).
/// </summary>
public class EntityManager
{
	private readonly Stack<int>       _availableIds = new();
	private          bool[]           _alive        = [];
	private          bool[]           _reserved     = [];
	private          EntityLocation[] _locations    = [];
	private          int              _nextId;
	private          int[]            _versions = [];

	/// <summary>
	///     Determines whether the specified entity is currently alive.
	/// </summary>
	/// <param name="entity">The entity to query.</param>
	/// <returns><c>true</c> if the entity is alive; otherwise, <c>false</c>.</returns>
	public bool IsAlive(Entity entity)
	{
		if (entity.Id < 0 || entity.Id >= _alive.Length) return false;

		return _alive[entity.Id] && _versions[entity.Id] == entity.Version;
	}

	/// <summary>
	///     Determines whether the specified entity is reserved for deferred creation.
	/// </summary>
	public bool IsReserved(Entity entity)
	{
		if (entity.Id < 0 || entity.Id >= _reserved.Length) return false;

		return _reserved[entity.Id] && _versions[entity.Id] == entity.Version;
	}

	/// <summary>
	///     Creates a new entity and marks it alive immediately.
	/// </summary>
	public Entity CreateEntity()
	{
		var entity = AllocateEntity(false);
		_alive[entity.Id] = true;
		return entity;
	}

	/// <summary>
	///     Reserves a new entity for deferred activation.
	/// </summary>
	public Entity ReserveEntity()
	{
		var entity = AllocateEntity(true);
		_reserved[entity.Id] = true;
		return entity;
	}

	/// <summary>
	///     Activates a reserved entity.
	/// </summary>
	public void ActivateReservedEntity(Entity entity)
	{
		EnsureReserved(entity);
		_reserved[entity.Id] = false;
		_alive[entity.Id]    = true;
	}

	/// <summary>
	///     Cancels a reserved entity without activating it.
	/// </summary>
	public void CancelReservedEntity(Entity entity)
	{
		EnsureReserved(entity);
		_reserved[entity.Id] = false;
		_versions[entity.Id]++;
		_locations[entity.Id] = EntityLocation.Empty;
		_availableIds.Push(entity.Id);
	}

	/// <summary>
	///     Destroys the specified entity and recycles its ID for future reuse.
	/// </summary>
	public void DestroyEntity(Entity entity)
	{
		EnsureAlive(entity);
		_alive[entity.Id] = false;
		_versions[entity.Id]++;
		_locations[entity.Id] = EntityLocation.Empty;
		_availableIds.Push(entity.Id);
	}

	internal EntityLocation GetLocation(Entity entity) =>
		entity.Id < 0 || entity.Id >= _locations.Length
			? EntityLocation.Empty
			: _locations[entity.Id];

	internal void EnsureAlive(Entity entity)
	{
		if (!IsAlive(entity))
			throw new InvalidOperationException($"Entity {entity.Id}:{entity.Version} is not alive.");
	}

	internal void SetLocation(Entity entity, EntityLocation location) =>
		_locations[entity.Id] = location;

	private Entity AllocateEntity(bool reserved)
	{
		int id = _availableIds.Count > 0 ? _availableIds.Pop() : _nextId++;
		EnsureCapacity(id);
		_alive[id]     = false;
		_reserved[id]  = reserved;
		_locations[id] = EntityLocation.Empty;
		return new(id, _versions[id]);
	}

	private void EnsureCapacity(int id)
	{
		if (id < _versions.Length) return;

		int newSize                   = _versions.Length == 0 ? 4 : _versions.Length;
		while (newSize <= id) newSize *= 2;

		Array.Resize(ref _alive,     newSize);
		Array.Resize(ref _reserved,  newSize);
		Array.Resize(ref _versions,  newSize);
		Array.Resize(ref _locations, newSize);
	}

	private void EnsureReserved(Entity entity)
	{
		if (!IsReserved(entity))
			throw new InvalidOperationException($"Entity {entity.Id}:{entity.Version} is not reserved.");
	}
}
