using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal readonly struct QuerySpec(
	int[]  allTypeIds,
	int[]  noneTypeIds,
	int[]  anyTypeIds,
	int[]  optionalTypeIds,
	int[]  changedTypeIds,
	Type?  relatedRelationType,
	Entity relatedTarget
)
{
	public Entity RelatedTarget { get; } = relatedTarget;

	public int[] AllTypeIds      { get; } = allTypeIds;
	public int[] AnyTypeIds      { get; } = anyTypeIds;
	public int[] ChangedTypeIds  { get; } = changedTypeIds;
	public int[] NoneTypeIds     { get; } = noneTypeIds;
	public int[] OptionalTypeIds { get; } = optionalTypeIds;

	public QueryCacheKey CacheKey => new(
		AllTypeIds,
		NoneTypeIds,
		AnyTypeIds,
		RelatedRelationType,
		RelatedTarget
	);

	public Type? RelatedRelationType { get; } = relatedRelationType;
}
