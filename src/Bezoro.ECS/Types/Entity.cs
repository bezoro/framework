using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Types;

/// <summary>
///     Represents a unique entity within the Entity Component System (ECS).
///     Each entity is identified by an immutable ID and version tuple.
/// </summary>
public readonly record struct Entity(int Id, int Version) : IEntity
{
	/// <summary>
	///     Represents an invalid entity handle.
	/// </summary>
	public static readonly Entity None = new(-1, 0);

	/// <summary>
	///     Represents a wildcard selector used by relationship queries.
	/// </summary>
	public static readonly Entity Wildcard = new(-2, 0);
}
