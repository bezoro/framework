namespace Bezoro.ECS.Types;

/// <summary>
///     Snapshot metrics for world-level archetype/chunk/entity memory usage.
/// </summary>
public sealed class WorldDiagnostics
{
	/// <summary>
	///     Initializes a new instance of the <see cref="WorldDiagnostics" /> class.
	/// </summary>
	/// <param name="archetypes">Per-archetype diagnostics.</param>
	/// <param name="entityCount">Total live entities in the world.</param>
	/// <param name="chunkCount">Total chunks allocated across archetypes.</param>
	/// <param name="allocatedBytes">Estimated allocated bytes across archetypes.</param>
	/// <param name="liveBytes">Estimated live bytes across archetypes.</param>
	public WorldDiagnostics(
		IReadOnlyList<ArchetypeDiagnostics> archetypes,
		int                                 entityCount,
		int                                 chunkCount,
		long                                allocatedBytes,
		long                                liveBytes)
	{
		if (archetypes is null)
			throw new ArgumentNullException(nameof(archetypes));

		if (entityCount < 0)
			throw new ArgumentOutOfRangeException(nameof(entityCount));

		if (chunkCount < 0)
			throw new ArgumentOutOfRangeException(nameof(chunkCount));

		if (allocatedBytes < 0)
			throw new ArgumentOutOfRangeException(nameof(allocatedBytes));

		if (liveBytes < 0)
			throw new ArgumentOutOfRangeException(nameof(liveBytes));

		Archetypes     = archetypes is ArchetypeDiagnostics[] array ? array : [.. archetypes];
		EntityCount    = entityCount;
		ChunkCount     = chunkCount;
		AllocatedBytes = allocatedBytes;
		LiveBytes      = liveBytes;
	}

	/// <summary>
	///     Gets the number of archetypes in the world.
	/// </summary>
	public int ArchetypeCount => Archetypes.Count;

	/// <summary>
	///     Gets the total number of allocated chunks in the world.
	/// </summary>
	public int ChunkCount { get; }

	/// <summary>
	///     Gets the total number of live entities in the world.
	/// </summary>
	public int EntityCount { get; }

	/// <summary>
	///     Gets diagnostics per archetype.
	/// </summary>
	public IReadOnlyList<ArchetypeDiagnostics> Archetypes { get; }

	/// <summary>
	///     Gets the estimated allocated bytes across all archetypes.
	/// </summary>
	public long AllocatedBytes { get; }

	/// <summary>
	///     Gets the estimated bytes occupied by live rows across all archetypes.
	/// </summary>
	public long LiveBytes { get; }
}
