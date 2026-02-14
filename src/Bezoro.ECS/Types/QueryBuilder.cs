using Bezoro.ECS.Internal.Fixed;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
///     Builds a compiled query plan for <see cref="World" />.
/// </summary>
public struct QueryBuilder
{
	private readonly World _world;
	private          int   _allCount;
	private          int   _anyCount;
	private          int   _noneCount;
	private          int[] _allTypeIds;
	private          int[] _anyTypeIds;
	private          int[] _noneTypeIds;

	internal QueryBuilder(World world)
	{
		_world       = world;
		_allTypeIds  = new int[4];
		_anyTypeIds  = new int[4];
		_noneTypeIds = new int[4];
		_allCount    = 0;
		_anyCount    = 0;
		_noneCount   = 0;
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
	///     Requires at least one of the registered <c>Any</c> component types.
	/// </summary>
	/// <typeparam name="T">Component type.</typeparam>
	public void Any<T>() where T : struct =>
		AddTypeId(_world.GetOrCreateComponentTypeId<T>(), ref _anyTypeIds, ref _anyCount);

	/// <summary>
	///     Excludes component type <typeparamref name="T" />.
	/// </summary>
	/// <typeparam name="T">Component type.</typeparam>
	public void None<T>() where T : struct =>
		AddTypeId(_world.GetOrCreateComponentTypeId<T>(), ref _noneTypeIds, ref _noneCount);

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
		int[]   allTypeIds    = FinalizeTypeIds(_allTypeIds,  _allCount);
		int[]   noneTypeIds   = FinalizeTypeIds(_noneTypeIds, _noneCount);
		int[]   anyTypeIds    = FinalizeTypeIds(_anyTypeIds,  _anyCount);
		ulong[] allMaskWords  = _world.BuildMaskWords(allTypeIds);
		ulong[] noneMaskWords = _world.BuildMaskWords(noneTypeIds);
		ulong[] anyMaskWords  = _world.BuildMaskWords(anyTypeIds);
		return new(
			_world,
			allTypeIds,
			noneTypeIds,
			anyTypeIds,
			allMaskWords,
			noneMaskWords,
			anyMaskWords,
			_world.BuildMaskWordIndices(allMaskWords),
			_world.BuildMaskWordIndices(noneMaskWords),
			_world.BuildMaskWordIndices(anyMaskWords)
		);
	}
}
