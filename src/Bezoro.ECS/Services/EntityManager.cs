using Bezoro.ECS.Types;

namespace Bezoro.ECS.Services;

/// <summary>
///     Manages the lifecycle of entities within the Entity Component System (ECS).
///     Provides methods to create and destroy entities while efficiently recycling entity IDs.
/// </summary>
public class EntityManager
{
	/// <summary>
	///     A queue containing reusable entity IDs from destroyed entities.
	/// </summary>
	private readonly Queue<int> availableIds = new();

	/// <summary>
	///     The next unique entity ID to be assigned if no reusable IDs are available.
	/// </summary>
	private int nextId;

	/// <summary>
	///     Creates a new entity. If there are recycled IDs available, reuses one;
	///     otherwise, generates a new unique ID.
	/// </summary>
	/// <returns>
	///     A new <see cref="Entity" /> instance with a unique identifier.
	/// </returns>
	public Entity CreateEntity()
	{
		int id = availableIds.Count > 0 ? availableIds.Dequeue() : nextId++;
		return new(id);
	}

	/// <summary>
	///     Destroys the specified entity and recycles its ID for future reuse.
	/// </summary>
	/// <param name="entity">The entity to destroy.</param>
	public void DestroyEntity(Entity entity) =>
		availableIds.Enqueue(entity.Id);
}
