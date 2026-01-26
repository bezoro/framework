namespace Bezoro.GameSystems.HealthSystem;

/// <summary>
///     Defines how current health should be adjusted when max health changes.
/// </summary>
public enum MaxHealthUpdateMode
{
	ClampCurrent,
	PreservePercentage
}
