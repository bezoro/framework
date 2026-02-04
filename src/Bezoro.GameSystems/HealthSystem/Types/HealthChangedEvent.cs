using Bezoro.Core.Types;

namespace Bezoro.GameSystems.HealthSystem.Types;

/// <summary>
///     Event payload describing a health mutation and its before/after state.
/// </summary>
/// <param name="Target">The target associated with the change.</param>
/// <param name="Kind">The kind of change that occurred.</param>
/// <param name="Value">The value passed to the change operation.</param>
/// <param name="MaxUpdateMode">The max update mode used for max changes.</param>
/// <param name="OldCurrent">The current health before the change.</param>
/// <param name="OldMax">The max health before the change.</param>
/// <param name="OldExcess">The excess health before the change.</param>
/// <param name="NewCurrent">The current health after the change.</param>
/// <param name="NewMax">The max health after the change.</param>
/// <param name="NewExcess">The excess health after the change.</param>
public readonly record struct HealthChangedEvent(
	object              Target,
	HealthChangeKind    Kind,
	uint                Value,
	MaxValueUpdateMode MaxUpdateMode,
	uint                OldCurrent,
	uint                OldMax,
	uint                OldExcess,
	uint                NewCurrent,
	uint                NewMax,
	uint                NewExcess
)
{
	/// <summary>
	///     Gets whether any tracked value changed.
	/// </summary>
	public bool Changed => OldCurrent != NewCurrent || OldMax != NewMax || OldExcess != NewExcess;

	/// <summary>
	///     Signed delta of current health.
	/// </summary>
	public long DeltaCurrent => (long)NewCurrent - OldCurrent;

	/// <summary>
	///     Signed delta of excess health.
	/// </summary>
	public long DeltaExcess => (long)NewExcess - OldExcess;

	/// <summary>
	///     Signed delta of max health.
	/// </summary>
	public long DeltaMax => (long)NewMax - OldMax;
}
