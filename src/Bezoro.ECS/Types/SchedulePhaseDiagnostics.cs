namespace Bezoro.ECS.Types;

/// <summary>
///     Describes scheduler stages for one loop phase.
/// </summary>
/// <param name="loopPhase">Loop phase represented by this diagnostics node.</param>
/// <param name="stages">Stage diagnostics for this loop phase.</param>
public sealed class SchedulePhaseDiagnostics(SystemLoopPhase loopPhase, ScheduleStageDiagnostics[] stages)
{
	/// <summary>
	///     Gets the loop phase represented by this diagnostics node.
	/// </summary>
	public SystemLoopPhase LoopPhase { get; } = loopPhase;

	/// <summary>
	///     Gets stage diagnostics for this loop phase.
	/// </summary>
	public ScheduleStageDiagnostics[] Stages { get; } = stages ?? throw new ArgumentNullException(nameof(stages));
}
