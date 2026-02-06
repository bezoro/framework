using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal readonly struct QuerySpec
{
	public QuerySpec(
		int[] allTypeIds,
		int[] noneTypeIds,
		int[] anyTypeIds,
		int[] optionalTypeIds,
		int[] changedTypeIds,
		Type? relatedRelationType,
		Entity relatedTarget)
	{
		AllTypeIds = allTypeIds;
		NoneTypeIds = noneTypeIds;
		AnyTypeIds = anyTypeIds;
		OptionalTypeIds = optionalTypeIds;
		ChangedTypeIds = changedTypeIds;
		RelatedRelationType = relatedRelationType;
		RelatedTarget = relatedTarget;
	}

	public int[] AllTypeIds { get; }
	public int[] NoneTypeIds { get; }
	public int[] AnyTypeIds { get; }
	public int[] OptionalTypeIds { get; }
	public int[] ChangedTypeIds { get; }
	public Type? RelatedRelationType { get; }
	public Entity RelatedTarget { get; }

	public string CacheKey
	{
		get
		{
			var key = string.Join(',', AllTypeIds) + "|" +
			          string.Join(',', NoneTypeIds) + "|" +
			          string.Join(',', AnyTypeIds) + "|" +
			          string.Join(',', ChangedTypeIds);

			if (RelatedRelationType is null)
				return key + "|R:none";

			return key + "|R:" + RelatedRelationType.AssemblyQualifiedName + ":" + RelatedTarget.Id + ":" + RelatedTarget.Version;
		}
	}
}
