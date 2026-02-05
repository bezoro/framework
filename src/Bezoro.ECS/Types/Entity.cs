using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Types;

/// <summary>
///     Represents a unique entity within the Entity Component System (ECS).
///     Each entity is identified by an immutable integer ID and version.
/// </summary>
public readonly struct Entity : IEntity
{
	/// <summary>
	///     Initializes a new instance of the <see cref="Entity" /> struct with the specified ID and version.
	/// </summary>
	/// <param name="id">The unique integer identifier for this entity.</param>
	/// <param name="version">The version associated with this entity ID.</param>
	internal Entity(int id, int version)
	{
		Id      = id;
		Version = version;
	}

	/// <summary>
	///     Gets the unique integer identifier for this entity.
	/// </summary>
	public int Id { get; }

	/// <summary>
	///     Gets the version associated with this entity ID.
	/// </summary>
	public int Version { get; }
}
