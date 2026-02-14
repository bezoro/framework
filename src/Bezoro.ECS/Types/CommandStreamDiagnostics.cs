namespace Bezoro.ECS.Types;

/// <summary>
///     Immutable diagnostics for one <see cref="CommandStream" /> instance.
/// </summary>
public sealed class CommandStreamDiagnostics(
	int commandCapacity,
	int recordedCommands,
	int highWatermark,
	int overflowCount
)
{
	/// <summary>
	///     Configured command capacity.
	/// </summary>
	public int CommandCapacity { get; } = commandCapacity;

	/// <summary>
	///     Peak recorded command count observed since stream creation.
	/// </summary>
	public int HighWatermark { get; } = highWatermark;

	/// <summary>
	///     Number of overflow attempts.
	/// </summary>
	public int OverflowCount { get; } = overflowCount;

	/// <summary>
	///     Current recorded command count.
	/// </summary>
	public int RecordedCommands { get; } = recordedCommands;
}
