namespace Bezoro.ECS.Types;

/// <summary>
///     Snapshot of scheduler plan structure for diagnostics and tooling.
/// </summary>
/// <param name="registeredSystemCount">Number of registered systems.</param>
/// <param name="planBuildCount">Number of times the execution plan has been rebuilt.</param>
/// <param name="phases">Phase diagnostics in scheduler execution order.</param>
public sealed class ScheduleDiagnostics(
	int                     registeredSystemCount,
	int                     planBuildCount,
	SchedulePhaseDiagnostics[] phases)
{
	/// <summary>
	///     Gets number of times the execution plan has been rebuilt.
	/// </summary>
	public int PlanBuildCount { get; } = planBuildCount;

	/// <summary>
	///     Gets phase diagnostics in scheduler execution order.
	/// </summary>
	public SchedulePhaseDiagnostics[] Phases { get; } = phases ?? throw new ArgumentNullException(nameof(phases));

	/// <summary>
	///     Gets number of registered systems.
	/// </summary>
	public int RegisteredSystemCount { get; } = registeredSystemCount;
}
