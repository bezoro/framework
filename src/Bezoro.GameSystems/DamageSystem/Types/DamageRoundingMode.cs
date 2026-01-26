namespace Bezoro.GameSystems.DamageSystem.Types;

/// <summary>
///     Rounding modes applied when converting raw damage to an integer value.
/// </summary>
public enum DamageRoundingMode
{
	RoundToNearest,
	RoundToEven,
	Floor,
	Ceiling,
	Truncate
}
