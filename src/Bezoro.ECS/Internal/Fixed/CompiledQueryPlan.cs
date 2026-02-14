using Bezoro.ECS.Services;

namespace Bezoro.ECS.Internal.Fixed;

internal sealed class CompiledQueryPlan(
	World   owner,
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
	private int   _matchingArchetypeCount;
	private int[] _matchingArchetypeIds = [];

	public int[] AllMaskWordIndices { get; } = allMaskWordIndices;

	public int[] AllTypeIds { get; } = allTypeIds;

	public int[] AnyMaskWordIndices { get; } = anyMaskWordIndices;

	public int[] AnyTypeIds { get; } = anyTypeIds;

	public int[] NoneMaskWordIndices { get; } = noneMaskWordIndices;

	public int[] NoneTypeIds { get; } = noneTypeIds;

	public ulong[] AllMaskWords { get; } = allMaskWords;

	public ulong[] AnyMaskWords { get; } = anyMaskWords;

	public ulong[] NoneMaskWords { get; } = noneMaskWords;

	public World Owner { get; } = owner;

	public int ArchetypeCacheVersion { get; set; } = -1;

	public int MatchingArchetypeCount
	{
		get => _matchingArchetypeCount;
		set => _matchingArchetypeCount = value < 0 ? 0 : value;
	}

	public int[] MatchingArchetypeIds
	{
		get => _matchingArchetypeIds;
		set => _matchingArchetypeIds = value ?? [];
	}
}
