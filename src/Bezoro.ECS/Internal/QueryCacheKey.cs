using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal readonly struct QueryCacheKey : IEquatable<QueryCacheKey>
{
	private readonly int[] _allTypeIds;
	private readonly int[] _noneTypeIds;
	private readonly int[] _anyTypeIds;
	private readonly Type? _relatedRelationType;
	private readonly Entity _relatedTarget;

	public QueryCacheKey(
		int[] allTypeIds,
		int[] noneTypeIds,
		int[] anyTypeIds,
		Type? relatedRelationType,
		Entity relatedTarget)
	{
		_allTypeIds = allTypeIds;
		_noneTypeIds = noneTypeIds;
		_anyTypeIds = anyTypeIds;
		_relatedRelationType = relatedRelationType;
		_relatedTarget = relatedTarget;
	}

	public bool Equals(QueryCacheKey other)
	{
		return _relatedRelationType == other._relatedRelationType &&
		       _relatedTarget == other._relatedTarget &&
		       SequenceEquals(_allTypeIds, other._allTypeIds) &&
		       SequenceEquals(_noneTypeIds, other._noneTypeIds) &&
		       SequenceEquals(_anyTypeIds, other._anyTypeIds);
	}

	public override bool Equals(object? obj) =>
		obj is QueryCacheKey other && Equals(other);

	public override int GetHashCode()
	{
		var hash = new HashCode();
		Add(ref hash, _allTypeIds);
		Add(ref hash, _noneTypeIds);
		Add(ref hash, _anyTypeIds);
		hash.Add(_relatedRelationType);
		hash.Add(_relatedTarget);
		return hash.ToHashCode();
	}

	private static bool SequenceEquals(int[] left, int[] right)
	{
		if (ReferenceEquals(left, right))
			return true;
		if (left.Length != right.Length)
			return false;

		for (var i = 0; i < left.Length; i++)
		{
			if (left[i] != right[i])
				return false;
		}

		return true;
	}

	private static void Add(ref HashCode hash, int[] values)
	{
		for (var i = 0; i < values.Length; i++)
			hash.Add(values[i]);
	}
}
