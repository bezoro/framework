namespace Bezoro.GameSystems.DamageSystem.Types;

/// <summary>
///     Represents a single typed damage component.
/// </summary>
public readonly struct DamageComponent(DamageType type, float amount)
{
	/// <summary>
	///     Gets the damage type for this component.
	/// </summary>
	public readonly DamageType Type = type;

	/// <summary>
	///     Gets the raw amount for this component.
	/// </summary>
	public readonly float Amount = amount;

	/// <inheritdoc />
	public override string ToString() => $"{Amount} {Type}";
}
