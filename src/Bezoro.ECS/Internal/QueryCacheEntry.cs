using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class QueryCacheEntry(QuerySpec spec)
{
	public List<Archetype> MatchingArchetypes { get; } = [];

	public QuerySpec Spec { get; } = spec;
}
