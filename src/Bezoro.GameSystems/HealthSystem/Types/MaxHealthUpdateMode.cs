namespace Bezoro.GameSystems.HealthSystem.Types;

/// <summary>
///     Defines how current health should be adjusted when max health changes.
/// </summary>
public enum MaxHealthUpdateMode
{
	ClampCurrent,
	PreservePercentage
}
