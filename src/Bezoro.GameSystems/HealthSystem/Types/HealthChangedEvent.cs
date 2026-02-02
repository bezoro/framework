using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.HealthSystem.Types;

/// <summary>
///     Event payload describing a health mutation and its before/after state.
/// </summary>
public readonly record struct HealthChangedEvent(
	IHealth             Target,
	HealthChangeKind    Kind,
	uint                Value,
	MaxHealthUpdateMode MaxUpdateMode,
	bool                SupportsExcess,
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
