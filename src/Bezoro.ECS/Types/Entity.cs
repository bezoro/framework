using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Types;

/// <summary>
///     Represents a unique entity within the Entity Component System (ECS).
///     Each entity is identified by an immutable world, ID, and version tuple.
/// </summary>
public readonly struct Entity : IEntity, IEquatable<Entity>
{
	/// <summary>
	///     Represents an invalid entity handle.
	/// </summary>
	public static readonly Entity None = new(-1, 0, -1);

	/// <summary>
	///     Represents a wildcard selector used by relationship queries.
	/// </summary>
	public static readonly Entity Wildcard = new(-2, 0, -1);

	/// <summary>
	///     Initializes a new instance of the <see cref="Entity" /> struct with the specified ID and version.
	/// </summary>
	/// <param name="id">The unique integer identifier for this entity.</param>
	/// <param name="version">The version associated with this entity ID.</param>
	internal Entity(int id, int version) : this(id, version, 0) { }

	internal Entity(int id, int version, int worldId)
	{
		Id      = id;
		Version = version;
		WorldId = worldId;
	}

	/// <summary>
	///     Gets the unique integer identifier for this entity.
	/// </summary>
	public int Id { get; }

	/// <summary>
	///     Gets the version associated with this entity ID.
	/// </summary>
	public int Version { get; }

	internal int WorldId { get; }

	/// <inheritdoc />
	public bool Equals(Entity other) =>
		Id == other.Id && Version == other.Version && WorldId == other.WorldId;

	/// <inheritdoc />
	public override bool Equals(object? obj) =>
		obj is Entity other && Equals(other);

	/// <inheritdoc />
	public override int GetHashCode() =>
		HashCode.Combine(Id, Version, WorldId);

	/// <summary>Determines whether two entities are equal.</summary>
	public static bool operator ==(Entity left, Entity right) => left.Equals(right);

	/// <summary>Determines whether two entities are not equal.</summary>
	public static bool operator !=(Entity left, Entity right) => !left.Equals(right);
}
