using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class QueryCacheEntry
{
	public QueryCacheEntry(QuerySpec spec)
	{
		Spec               = spec;
		MatchingArchetypes = [];
	}

	public List<Archetype> MatchingArchetypes { get; }

	public QuerySpec Spec { get; }
}
