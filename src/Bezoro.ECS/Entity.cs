namespace Bezoro.ECS;

/// <summary>
///     Represents a unique entity within the Entity Component System (ECS).
///     Each entity is identified by an immutable integer ID.
/// </summary>
public readonly struct Entity : IEntity
{
	/// <summary>
	///     Initializes a new instance of the <see cref="Entity" /> struct with the specified ID.
	/// </summary>
	/// <param name="id">The unique integer identifier for this entity.</param>
	internal Entity(int id)
	{
		Id = id;
	}

	/// <summary>
	///     Gets the unique integer identifier for this entity.
	/// </summary>
	public int Id { get; }
}
