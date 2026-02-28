using Bezoro.ECS.Internal.Fixed;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
///     Builds a compiled query plan for <see cref="World" />.
/// </summary>
public struct QueryBuilder
{
	private readonly World _world;
	private          int   _addedCount;
	private          int   _allCount;
	private          int   _anyCount;
	private          int   _changedCount;
	private          bool  _hasRelatedFilter;
	private          int   _noneCount;
	private          int   _optionalCount;
	private          Type? _relatedRelationType;
	private          int[] _addedTypeIds;
	private          int[] _allTypeIds;
	private          int[] _anyTypeIds;
	private          int[] _changedTypeIds;
	private          int[] _noneTypeIds;
	private          int[] _optionalTypeIds;
	private          Entity _relatedTarget;

	internal QueryBuilder(World world)
	{
		_world       = world;
		_addedTypeIds    = new int[4];
		_allTypeIds      = new int[4];
		_anyTypeIds      = new int[4];
		_noneTypeIds     = new int[4];
		_optionalTypeIds = new int[4];
		_changedTypeIds  = new int[4];
		_addedCount      = 0;
		_allCount        = 0;
		_anyCount        = 0;
		_noneCount       = 0;
		_optionalCount   = 0;
		_changedCount    = 0;
		_relatedRelationType = null;
		_relatedTarget       = Entity.None;
		_hasRelatedFilter    = false;
	}

	internal CompiledQueryPlan Build() =>
		BuildPlan();

	/// <summary>
	///     Requires component type <typeparamref name="T" />.
	/// </summary>
	/// <typeparam name="T">Component type.</typeparam>
	public void All<T>() where T : struct =>
		AddTypeId(_world.GetOrCreateComponentTypeId<T>(), ref _allTypeIds, ref _allCount);

	/// <summary>
	///     Requires component type <typeparamref name="T" />.
	/// </summary>
	/// <typeparam name="T">Component type.</typeparam>
	public void With<T>() where T : struct =>
		All<T>();

	/// <summary>
	///     Requires at least one of the registered <c>Any</c> component types.
	/// </summary>
	/// <typeparam name="T">Component type.</typeparam>
	public void Any<T>() where T : struct =>
		AddTypeId(_world.GetOrCreateComponentTypeId<T>(), ref _anyTypeIds, ref _anyCount);

	/// <summary>
	///     Requires at least one of the registered <c>AnyOf</c> component types.
	/// </summary>
	/// <typeparam name="T">Component type.</typeparam>
	public void AnyOf<T>() where T : struct =>
		Any<T>();

	/// <summary>
	///     Excludes component type <typeparamref name="T" />.
	/// </summary>
	/// <typeparam name="T">Component type.</typeparam>
	public void None<T>() where T : struct =>
		AddTypeId(_world.GetOrCreateComponentTypeId<T>(), ref _noneTypeIds, ref _noneCount);

	/// <summary>
	///     Excludes component type <typeparamref name="T" />.
	/// </summary>
	/// <typeparam name="T">Component type.</typeparam>
	public void Without<T>() where T : struct =>
		None<T>();

	/// <summary>
	///     Registers component type <typeparamref name="T" /> as optional.
	/// </summary>
	/// <typeparam name="T">Component type.</typeparam>
	public void Optional<T>() where T : struct =>
		AddTypeId(_world.GetOrCreateComponentTypeId<T>(), ref _optionalTypeIds, ref _optionalCount);

	/// <summary>
	///     Requires component type <typeparamref name="T" /> to have been added since the previous query execution.
	/// </summary>
	/// <typeparam name="T">Component type.</typeparam>
	public void Added<T>() where T : struct =>
		AddTypeId(_world.GetOrCreateComponentTypeId<T>(), ref _addedTypeIds, ref _addedCount);

	/// <summary>
	///     Requires component type <typeparamref name="T" /> to have changed since the previous query execution.
	/// </summary>
	/// <typeparam name="T">Component type.</typeparam>
	public void Changed<T>() where T : struct =>
		AddTypeId(_world.GetOrCreateComponentTypeId<T>(), ref _changedTypeIds, ref _changedCount);

	/// <summary>
	///     Requires a relationship of type <typeparamref name="TRelation" /> to the specified <paramref name="target" />.
	/// </summary>
	/// <typeparam name="TRelation">Relationship tag type.</typeparam>
	/// <param name="target">Relationship target entity or <see cref="Entity.Wildcard" /> for any target.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="target" /> is <see cref="Entity.None" />.</exception>
	/// <exception cref="InvalidOperationException">Thrown when a different relation filter was already configured.</exception>
	public void Related<TRelation>(Entity target) where TRelation : struct
	{
		if (target == Entity.None)
			throw new ArgumentException("Related target cannot be Entity.None.", nameof(target));

		Type relationType = typeof(TRelation);
		if (!_hasRelatedFilter)
		{
			_relatedRelationType = relationType;
			_relatedTarget       = target;
			_hasRelatedFilter    = true;
			return;
		}

		if (_relatedRelationType != relationType || _relatedTarget != target)
			throw new InvalidOperationException(
				"Only one relation filter may be configured per compiled query."
			);
	}

	/// <summary>
	///     Requires a relationship of type <typeparamref name="TRelation" /> to any target.
	/// </summary>
	/// <typeparam name="TRelation">Relationship tag type.</typeparam>
	public void Related<TRelation>() where TRelation : struct =>
		Related<TRelation>(Entity.Wildcard);

	private static int[] MergeSortedTypeIds(int[] first, int[] second)
	{
		if (first.Length == 0)
			return second;

		if (second.Length == 0)
			return first;

		var merged = new int[first.Length + second.Length];
		var i      = 0;
		var j      = 0;
		var write  = 0;
		while (i < first.Length && j < second.Length)
		{
			int left  = first[i];
			int right = second[j];
			if (left == right)
			{
				merged[write++] = left;
				i++;
				j++;
				continue;
			}

			if (left < right)
			{
				merged[write++] = left;
				i++;
				continue;
			}

			merged[write++] = right;
			j++;
		}

		while (i < first.Length)
			merged[write++] = first[i++];

		while (j < second.Length)
			merged[write++] = second[j++];

		if (write == merged.Length)
			return merged;

		var resized = new int[write];
		Array.Copy(merged, resized, write);
		return resized;
	}

	private static int[] FinalizeTypeIds(int[] buffer, int count)
	{
		if (count == 0)
			return [];

		var copy = new int[count];
		Array.Copy(buffer, copy, count);
		Array.Sort(copy);
		return copy;
	}

	private static void AddTypeId(int typeId, ref int[] buffer, ref int count)
	{
		for (var i = 0; i < count; i++)
		{
			if (buffer[i] == typeId)
				return;
		}

		if (count == buffer.Length)
			Array.Resize(ref buffer, buffer.Length * 2);

		buffer[count++] = typeId;
	}

	private CompiledQueryPlan BuildPlan()
	{
		int[] addedTypeIds    = FinalizeTypeIds(_addedTypeIds,    _addedCount);
		int[] changedTypeIds  = FinalizeTypeIds(_changedTypeIds,  _changedCount);
		int[] allTypeIds      = FinalizeTypeIds(_allTypeIds,      _allCount);
		int[] requiredTypeIds = MergeSortedTypeIds(allTypeIds, MergeSortedTypeIds(addedTypeIds, changedTypeIds));
		int[] noneTypeIds     = FinalizeTypeIds(_noneTypeIds,      _noneCount);
		int[] anyTypeIds      = FinalizeTypeIds(_anyTypeIds,       _anyCount);
		int[] optionalTypeIds = FinalizeTypeIds(_optionalTypeIds,  _optionalCount);
		ulong[] allMaskWords  = _world.BuildMaskWords(requiredTypeIds);
		ulong[] noneMaskWords = _world.BuildMaskWords(noneTypeIds);
		ulong[] anyMaskWords  = _world.BuildMaskWords(anyTypeIds);
		return new(
			_world,
			requiredTypeIds,
			noneTypeIds,
			anyTypeIds,
			optionalTypeIds,
			addedTypeIds,
			changedTypeIds,
			allMaskWords,
			noneMaskWords,
			anyMaskWords,
			_world.BuildMaskWordIndices(allMaskWords),
			_world.BuildMaskWordIndices(noneMaskWords),
			_world.BuildMaskWordIndices(anyMaskWords),
			_relatedRelationType,
			_relatedTarget
		);
	}
}
