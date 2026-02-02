using Bezoro.GameSystems.HealthSystem.Types;

namespace Bezoro.GameSystems.HealthSystem.Extensions;

/// <summary>
///     Helpers for classifying <see cref="HealthChangeKind" /> values.
///     These describe the requested operation, not necessarily the resulting deltas.
/// </summary>
public static class HealthChangeKindExtensions
{
	/// <summary>
	///     Returns true if the operation targets current health.
	/// </summary>
	public static bool TargetsCurrent(this HealthChangeKind kind) =>
		kind is HealthChangeKind.DecreaseCurrent
			or HealthChangeKind.IncreaseCurrent
			or HealthChangeKind.RestoreCurrent
			or HealthChangeKind.DepleteCurrent
			or HealthChangeKind.FullyRestoreCurrent
			or HealthChangeKind.SetCurrent;

	/// <summary>
	///     Returns true if the operation targets max health.
	/// </summary>
	public static bool TargetsMax(this HealthChangeKind kind) =>
		kind is HealthChangeKind.DecreaseMax
			or HealthChangeKind.IncreaseMax
			or HealthChangeKind.SetMax;

	/// <summary>
	///     Returns true if the operation targets excess health.
	/// </summary>
	public static bool TargetsExcess(this HealthChangeKind kind) =>
		kind is HealthChangeKind.DepleteExcess
			or HealthChangeKind.DecreaseExcess
			or HealthChangeKind.IncreaseExcess
			or HealthChangeKind.SetExcess;

	/// <summary>
	///     Returns true if the operation is an increase.
	/// </summary>
	public static bool IsIncrease(this HealthChangeKind kind) =>
		kind is HealthChangeKind.IncreaseCurrent
			or HealthChangeKind.IncreaseMax
			or HealthChangeKind.IncreaseExcess;

	/// <summary>
	///     Returns true if the operation is a decrease.
	/// </summary>
	public static bool IsDecrease(this HealthChangeKind kind) =>
		kind is HealthChangeKind.DecreaseCurrent
			or HealthChangeKind.DecreaseMax
			or HealthChangeKind.DecreaseExcess;

	/// <summary>
	///     Returns true if the operation sets a value directly.
	/// </summary>
	public static bool IsSet(this HealthChangeKind kind) =>
		kind is HealthChangeKind.SetCurrent
			or HealthChangeKind.SetMax
			or HealthChangeKind.SetExcess;

	/// <summary>
	///     Returns true if the operation is a restore to current health.
	/// </summary>
	public static bool IsRestore(this HealthChangeKind kind) =>
		kind is HealthChangeKind.RestoreCurrent
			or HealthChangeKind.FullyRestoreCurrent;

	/// <summary>
	///     Returns true if the operation depletes a health pool.
	/// </summary>
	public static bool IsDeplete(this HealthChangeKind kind) =>
		kind is HealthChangeKind.DepleteCurrent
			or HealthChangeKind.DepleteExcess;
}
