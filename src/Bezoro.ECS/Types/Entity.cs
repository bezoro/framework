using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Types;

/// <summary>
///     Represents a unique entity within the Entity Component System (ECS).
///     Each entity is identified by an immutable integer ID and version.
/// </summary>
public readonly struct Entity : IEntity, IEquatable<Entity>
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

	/// <inheritdoc />
	public bool Equals(Entity other) =>
		Id == other.Id && Version == other.Version;

	/// <inheritdoc />
	public override bool Equals(object? obj) =>
		obj is Entity other && Equals(other);

	/// <inheritdoc />
	public override int GetHashCode() =>
		HashCode.Combine(Id, Version);

	/// <summary>Determines whether two entities are equal.</summary>
	public static bool operator ==(Entity left, Entity right) => left.Equals(right);

	/// <summary>Determines whether two entities are not equal.</summary>
	public static bool operator !=(Entity left, Entity right) => !left.Equals(right);
}
