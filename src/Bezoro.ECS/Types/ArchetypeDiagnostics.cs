namespace Bezoro.ECS.Types;

/// <summary>
/// Snapshot metrics for a single archetype.
/// </summary>
public sealed class ArchetypeDiagnostics
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ArchetypeDiagnostics" /> class.
	/// </summary>
	/// <param name="archetypeId">Archetype identifier inside the world.</param>
	/// <param name="chunkCount">Number of chunks allocated for the archetype.</param>
	/// <param name="chunkCapacity">Entity capacity per chunk.</param>
	/// <param name="entityCount">Number of live entities in the archetype.</param>
	/// <param name="allocatedEntitySlots">Total allocated entity slots for the archetype.</param>
	/// <param name="bytesPerEntity">Estimated bytes used per entity row (entity handle + components).</param>
	/// <param name="allocatedBytes">Estimated bytes allocated for all slots.</param>
	/// <param name="liveBytes">Estimated bytes used by live rows.</param>
	/// <param name="componentTypes">Component types that define the archetype.</param>
	public ArchetypeDiagnostics(
		int archetypeId,
		int chunkCount,
		int chunkCapacity,
		int entityCount,
		int allocatedEntitySlots,
		long bytesPerEntity,
		long allocatedBytes,
		long liveBytes,
		IReadOnlyList<Type> componentTypes)
	{
		if (chunkCount < 0)
			throw new ArgumentOutOfRangeException(nameof(chunkCount));
		if (chunkCapacity < 0)
			throw new ArgumentOutOfRangeException(nameof(chunkCapacity));
		if (entityCount < 0)
			throw new ArgumentOutOfRangeException(nameof(entityCount));
		if (allocatedEntitySlots < 0)
			throw new ArgumentOutOfRangeException(nameof(allocatedEntitySlots));
		if (bytesPerEntity < 0)
			throw new ArgumentOutOfRangeException(nameof(bytesPerEntity));
		if (allocatedBytes < 0)
			throw new ArgumentOutOfRangeException(nameof(allocatedBytes));
		if (liveBytes < 0)
			throw new ArgumentOutOfRangeException(nameof(liveBytes));
		if (componentTypes is null)
			throw new ArgumentNullException(nameof(componentTypes));

		ArchetypeId = archetypeId;
		ChunkCount = chunkCount;
		ChunkCapacity = chunkCapacity;
		EntityCount = entityCount;
		AllocatedEntitySlots = allocatedEntitySlots;
		BytesPerEntity = bytesPerEntity;
		AllocatedBytes = allocatedBytes;
		LiveBytes = liveBytes;
		ComponentTypes = componentTypes is Type[] array ? array : [.. componentTypes];
	}

	/// <summary>
	/// Gets the archetype identifier inside the world.
	/// </summary>
	public int ArchetypeId { get; }

	/// <summary>
	/// Gets the number of chunks allocated for this archetype.
	/// </summary>
	public int ChunkCount { get; }

	/// <summary>
	/// Gets the number of entity slots per chunk.
	/// </summary>
	public int ChunkCapacity { get; }

	/// <summary>
	/// Gets the number of live entities currently stored in this archetype.
	/// </summary>
	public int EntityCount { get; }

	/// <summary>
	/// Gets the total number of allocated entity slots.
	/// </summary>
	public int AllocatedEntitySlots { get; }

	/// <summary>
	/// Gets the estimated bytes used per entity row.
	/// </summary>
	public long BytesPerEntity { get; }

	/// <summary>
	/// Gets the estimated bytes allocated by this archetype.
	/// </summary>
	public long AllocatedBytes { get; }

	/// <summary>
	/// Gets the estimated bytes occupied by live rows in this archetype.
	/// </summary>
	public long LiveBytes { get; }

	/// <summary>
	/// Gets the component types that define this archetype.
	/// </summary>
	public IReadOnlyList<Type> ComponentTypes { get; }
}
