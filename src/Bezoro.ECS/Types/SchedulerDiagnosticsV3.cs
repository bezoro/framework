namespace Bezoro.ECS.Types;

/// <summary>
/// Captures scheduler plan diagnostics for <see cref="Services.WorldV3" />.
/// </summary>
/// <param name="RegisteredSystems">Number of registered systems.</param>
/// <param name="WaveCount">Number of planned execution waves.</param>
/// <param name="MaxWaveWidth">Maximum systems in any wave.</param>
/// <param name="PlanRebuildCount">Number of times the wave plan has been rebuilt.</param>
public readonly record struct SchedulerDiagnosticsV3(
	int RegisteredSystems,
	int WaveCount,
	int MaxWaveWidth,
	int PlanRebuildCount
);
