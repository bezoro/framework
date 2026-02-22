namespace Bezoro.ECS.Types;

/// <summary>
///     Diagnostics for a compiled query plan and its current match footprint.
/// </summary>
public sealed class QueryDiagnostics(
	int   matchingArchetypeCount,
	int   matchingChunkCount,
	int   matchingEntityCount,
	int   archetypeCacheVersion,
	bool  isArchetypeCacheUpToDate,
	Type[] allTypes,
	Type[] anyTypes,
	Type[] noneTypes,
	Type[] optionalTypes,
	Type[] addedTypes,
	Type[] changedTypes,
	Type? relatedRelationType,
	Entity relatedTarget)
{
	/// <summary>
	///     Types required by the query (<c>All</c>, including <c>Added</c>/<c>Changed</c> promoted requirements).
	/// </summary>
	public Type[] AllTypes { get; } = allTypes ?? throw new ArgumentNullException(nameof(allTypes));

	/// <summary>
	///     Additional <c>Any</c> filter types.
	/// </summary>
	public Type[] AnyTypes { get; } = anyTypes ?? throw new ArgumentNullException(nameof(anyTypes));

	/// <summary>
	///     Current plan archetype cache version.
	/// </summary>
	public int ArchetypeCacheVersion { get; } = archetypeCacheVersion;

	/// <summary>
	///     Incremental <c>Added</c> filter types.
	/// </summary>
	public Type[] AddedTypes { get; } = addedTypes ?? throw new ArgumentNullException(nameof(addedTypes));

	/// <summary>
	///     Incremental <c>Changed</c> filter types.
	/// </summary>
	public Type[] ChangedTypes { get; } = changedTypes ?? throw new ArgumentNullException(nameof(changedTypes));

	/// <summary>
	///     Indicates whether the query archetype cache matches current world archetype version.
	/// </summary>
	public bool IsArchetypeCacheUpToDate { get; } = isArchetypeCacheUpToDate;

	/// <summary>
	///     Number of archetypes matching static query filters.
	/// </summary>
	public int MatchingArchetypeCount { get; } = matchingArchetypeCount;

	/// <summary>
	///     Number of chunk ranges matching dynamic + static filters for this diagnostics snapshot.
	/// </summary>
	public int MatchingChunkCount { get; } = matchingChunkCount;

	/// <summary>
	///     Number of entities matching dynamic + static filters for this diagnostics snapshot.
	/// </summary>
	public int MatchingEntityCount { get; } = matchingEntityCount;

	/// <summary>
	///     Excluded <c>None</c> filter types.
	/// </summary>
	public Type[] NoneTypes { get; } = noneTypes ?? throw new ArgumentNullException(nameof(noneTypes));

	/// <summary>
	///     Optional component types registered in the query.
	/// </summary>
	public Type[] OptionalTypes { get; } = optionalTypes ?? throw new ArgumentNullException(nameof(optionalTypes));

	/// <summary>
	///     Relation filter relation type, if configured.
	/// </summary>
	public Type? RelatedRelationType { get; } = relatedRelationType;

	/// <summary>
	///     Relation target for <see cref="RelatedRelationType" />.
	/// </summary>
	public Entity RelatedTarget { get; } = relatedTarget;
}
