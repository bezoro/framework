using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class QueryCacheEntry
{
	public QueryCacheEntry(QuerySpec spec)
	{
		Spec = spec;
		MatchingArchetypes = [];
	}

	public QuerySpec Spec { get; }

	public List<Archetype> MatchingArchetypes { get; }
}
