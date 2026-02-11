namespace Bezoro.GameSystems.HealthSystem.Types;

/// <summary>
///     Describes the mutation applied to a health component.
/// </summary>
public enum HealthChangeKind
{
	/// <summary>
	///     Damage was applied and consumed excess first.
	/// </summary>
	Damage,

	/// <summary>
	///     Healing was applied to base health only.
	/// </summary>
	Heal,

	/// <summary>
	///     Damage was applied directly to base health.
	/// </summary>
	DirectDamage,

	/// <summary>
	///     Healing was applied and overflowed into excess health.
	/// </summary>
	IncreaseHealth,

	/// <summary>
	///     Maximum health was set.
	/// </summary>
	SetMax
}
