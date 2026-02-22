namespace Bezoro.ECS.Types;

/// <summary>
///     Describes a scheduled system batch within a stage.
/// </summary>
/// <param name="batchIndex">Zero-based batch index within the stage.</param>
/// <param name="systemTypes">System types scheduled in this batch.</param>
/// <param name="containsExclusiveSystem">Indicates whether this batch contains an exclusive system.</param>
public sealed class ScheduleBatchDiagnostics(
	int     batchIndex,
	Type[]  systemTypes,
	bool containsExclusiveSystem)
{
	/// <summary>
	///     Gets the zero-based batch index within the stage.
	/// </summary>
	public int BatchIndex { get; } = batchIndex;

	/// <summary>
	///     Gets whether this batch contains an exclusive system.
	/// </summary>
	public bool ContainsExclusiveSystem { get; } = containsExclusiveSystem;

	/// <summary>
	///     Gets the system types scheduled in this batch.
	/// </summary>
	public Type[] SystemTypes { get; } = systemTypes ?? throw new ArgumentNullException(nameof(systemTypes));
}
