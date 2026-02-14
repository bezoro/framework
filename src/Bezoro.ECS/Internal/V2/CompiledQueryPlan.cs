using Bezoro.ECS.Services;

namespace Bezoro.ECS.Internal.V2;

internal sealed class CompiledQueryPlan(
	WorldV2 owner,
	int[]   allTypeIds,
	int[]   noneTypeIds,
	int[]   anyTypeIds,
	ulong[] allMaskWords,
	ulong[] noneMaskWords,
	ulong[] anyMaskWords,
	int[]   allMaskWordIndices,
	int[]   noneMaskWordIndices,
	int[]   anyMaskWordIndices
)
{
	private int[] _matchingArchetypeIds = [];
	private int   _matchingArchetypeCount;

	public WorldV2 Owner { get; } = owner;

	public int[] AllTypeIds { get; } = allTypeIds;

	public int[] NoneTypeIds { get; } = noneTypeIds;

	public int[] AnyTypeIds { get; } = anyTypeIds;

	public ulong[] AllMaskWords { get; } = allMaskWords;

	public ulong[] NoneMaskWords { get; } = noneMaskWords;

	public ulong[] AnyMaskWords { get; } = anyMaskWords;

	public int[] AllMaskWordIndices { get; } = allMaskWordIndices;

	public int[] NoneMaskWordIndices { get; } = noneMaskWordIndices;

	public int[] AnyMaskWordIndices { get; } = anyMaskWordIndices;

	public int ArchetypeCacheVersion { get; set; } = -1;

	public int[] MatchingArchetypeIds
	{
		get => _matchingArchetypeIds;
		set => _matchingArchetypeIds = value ?? [];
	}

	public int MatchingArchetypeCount
	{
		get => _matchingArchetypeCount;
		set => _matchingArchetypeCount = value < 0 ? 0 : value;
	}
}
