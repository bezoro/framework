namespace Bezoro.GameSystems.DamageSystem.Resistances;

/// <summary>
///     Defines a flat and multiplicative adjustment for a damage type.
/// </summary>
public readonly struct DamageResistance(float multiplier = 1f, float flat = 0f)
{
	/// <summary>
	///     No adjustment (multiplier = 1, flat = 0).
	/// </summary>
	public static readonly DamageResistance None = new(1f);

	/// <summary>
	///     Gets the flat amount added before the multiplier.
	/// </summary>
	public readonly float Flat = flat;
	/// <summary>
	///     Gets the multiplier applied after the flat adjustment.
	/// </summary>
	public readonly float Multiplier = multiplier;

	/// <summary>
	///     Applies this resistance to the provided amount.
	/// </summary>
	public float Apply(float amount) => (amount + Flat) * Multiplier;
}
