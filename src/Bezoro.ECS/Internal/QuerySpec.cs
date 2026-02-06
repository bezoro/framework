using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal readonly struct QuerySpec
{
	public QuerySpec(
		int[]  allTypeIds,
		int[]  noneTypeIds,
		int[]  anyTypeIds,
		int[]  optionalTypeIds,
		int[]  changedTypeIds,
		Type?  relatedRelationType,
		Entity relatedTarget)
	{
		AllTypeIds          = allTypeIds;
		NoneTypeIds         = noneTypeIds;
		AnyTypeIds          = anyTypeIds;
		OptionalTypeIds     = optionalTypeIds;
		ChangedTypeIds      = changedTypeIds;
		RelatedRelationType = relatedRelationType;
		RelatedTarget       = relatedTarget;
	}

	public Entity RelatedTarget { get; }

	public int[] AllTypeIds      { get; }
	public int[] AnyTypeIds      { get; }
	public int[] ChangedTypeIds  { get; }
	public int[] NoneTypeIds     { get; }
	public int[] OptionalTypeIds { get; }

	public QueryCacheKey CacheKey => new(
		AllTypeIds,
		NoneTypeIds,
		AnyTypeIds,
		RelatedRelationType,
		RelatedTarget
	);

	public Type? RelatedRelationType { get; }
}
