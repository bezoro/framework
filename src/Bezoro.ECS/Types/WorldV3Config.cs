namespace Bezoro.ECS.Types;

/// <summary>
/// Configures fixed capacities and scheduler settings for <see cref="Services.WorldV3" />.
/// </summary>
public sealed class WorldV3Config
{
	/// <summary>
	/// Maximum number of alive + recyclable entity slots.
	/// </summary>
	public int EntityCapacity { get; init; } = 131_072;

	/// <summary>
	/// Maximum number of registered component types.
	/// </summary>
	public int ComponentTypeCapacity { get; init; } = 256;

	/// <summary>
	/// Maximum number of commands recordable in one command stream.
	/// </summary>
	public int CommandCapacity { get; init; } = 131_072;

	/// <summary>
	/// Maximum payload entries per component type in a command stream.
	/// </summary>
	public int CommandPayloadCapacityPerType { get; init; } = 65_536;

	/// <summary>
	/// Maximum number of entities returned by one query execution.
	/// </summary>
	public int QueryResultCapacity { get; init; } = 131_072;

	/// <summary>
	/// Number of entity rows per archetype chunk.
	/// </summary>
	public int ChunkCapacity { get; init; } = 256;

	/// <summary>
	/// Overflow policy used by fixed-capacity buffers.
	/// </summary>
	public WorldOverflowPolicy OverflowPolicy { get; init; } = WorldOverflowPolicy.FailFast;

	/// <summary>
	/// Maximum worker count used by the parallel system scheduler.
	/// </summary>
	public int ParallelWorkerCount { get; init; } = Environment.ProcessorCount;

	internal void Validate()
	{
		if (EntityCapacity <= 0)
			throw new ArgumentOutOfRangeException(nameof(EntityCapacity), "Entity capacity must be positive.");

		if (ComponentTypeCapacity <= 0)
			throw new ArgumentOutOfRangeException(nameof(ComponentTypeCapacity), "Component type capacity must be positive.");

		if (CommandCapacity <= 0)
			throw new ArgumentOutOfRangeException(nameof(CommandCapacity), "Command capacity must be positive.");

		if (CommandPayloadCapacityPerType <= 0)
			throw new ArgumentOutOfRangeException(
				nameof(CommandPayloadCapacityPerType),
				"Command payload capacity must be positive."
			);

		if (QueryResultCapacity <= 0)
			throw new ArgumentOutOfRangeException(nameof(QueryResultCapacity), "Query result capacity must be positive.");

		if (ChunkCapacity <= 0)
			throw new ArgumentOutOfRangeException(nameof(ChunkCapacity), "Chunk capacity must be positive.");

		if (ParallelWorkerCount <= 0)
			throw new ArgumentOutOfRangeException(nameof(ParallelWorkerCount), "Parallel worker count must be positive.");
	}
}
