using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Represents a generated query definition that can build a concrete query from a world.
/// </summary>
public interface IQuery
{
	/// <summary>
	///     Creates a concrete query instance for the provided world.
	/// </summary>
	/// <param name="world">World to build the query against.</param>
	/// <returns>The built query.</returns>
	Query Create(IWorld world);
}
