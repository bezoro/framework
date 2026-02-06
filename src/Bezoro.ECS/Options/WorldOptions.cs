namespace Bezoro.ECS.Options;

/// <summary>
///     Configures performance-related settings for a world instance.
/// </summary>
public sealed class WorldOptions
{
	/// <summary>
	///     Gets or initializes an explicit number of entities per chunk.
	///     Set to a positive value to override byte-budget-based sizing.
	/// </summary>
	public int ChunkCapacity { get; set; }

	/// <summary>
	///     Gets or initializes the target chunk size in bytes used for automatic capacity calculation.
	/// </summary>
	public int ChunkSizeInBytes { get; set; } = 16 * 1024;

	/// <summary>
	///     Gets or initializes the maximum degree of parallelism for system updates.
	/// </summary>
	public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
}
