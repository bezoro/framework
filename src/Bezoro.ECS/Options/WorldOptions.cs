namespace Bezoro.ECS.Options;

/// <summary>
///     Provides compatibility settings for constructing a world.
/// </summary>
/// <remarks>
///     New code should use <see cref="Bezoro.ECS.Types.WorldConfig" />. The compatibility adapter maps a positive
///     <see cref="ChunkCapacity" /> exactly, maps a nonpositive value to 256, preserves
///     <see cref="MaxDegreeOfParallelism" /> exactly, and ignores <see cref="ChunkSizeInBytes" />.
/// </remarks>
[Obsolete("Use WorldConfig instead.")]
public sealed class WorldOptions
{
	/// <summary>
	///     Gets or sets the requested number of entities per chunk.
	/// </summary>
	/// <remarks>A positive value is mapped exactly; a nonpositive value is mapped to 256.</remarks>
	public int ChunkCapacity { get; set; }

	/// <summary>
	///     Gets or sets a legacy byte-size hint retained for source compatibility.
	/// </summary>
	/// <remarks>The compatibility adapter ignores this value.</remarks>
	public int ChunkSizeInBytes { get; set; } = 16 * 1024;

	/// <summary>
	///     Gets or sets the maximum degree of parallelism for system updates.
	/// </summary>
	/// <remarks>The compatibility adapter preserves this value exactly; it must be positive.</remarks>
	public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
}
