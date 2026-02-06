using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Services;

/// <summary>
///     Provides query-definition based query creation extensions.
/// </summary>
public static class WorldQueryExtensions
{
	/// <summary>
	///     Builds a query from a source-generated query definition.
	/// </summary>
	/// <typeparam name="TQuery">Generated query definition type.</typeparam>
	/// <param name="world">World to query.</param>
	/// <returns>A concrete query configured by <typeparamref name="TQuery" />.</returns>
	public static Query Query<TQuery>(this IWorld world)
		where TQuery : struct, IQuery
	{
		if (world is null) throw new ArgumentNullException(nameof(world));

		return default(TQuery).Create(world);
	}
}
