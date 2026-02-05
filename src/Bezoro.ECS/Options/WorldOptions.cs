namespace Bezoro.ECS.Options;

/// <summary>
///     Configures performance-related settings for a world instance.
/// </summary>
public sealed class WorldOptions
{
	/// <summary>
	///     Gets or initializes the number of entities per chunk.
	/// </summary>
	public int ChunkCapacity { get; set; } = 128;

	/// <summary>
	///     Gets or initializes the maximum degree of parallelism for system updates.
	/// </summary>
	public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
}
