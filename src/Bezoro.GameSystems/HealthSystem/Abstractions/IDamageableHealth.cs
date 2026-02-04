namespace Bezoro.GameSystems.HealthSystem.Abstractions;

/// <summary>
///     Defines the minimum health operations required for damage application.
/// </summary>
/// <typeparam name="TSelf">The implementing health type.</typeparam>
public interface IDamageableHealth<TSelf>
	where TSelf : struct, IDamageableHealth<TSelf>
{
	/// <summary>
	///     Gets the effective current health used for damage calculations.
	/// </summary>
	uint EffectiveCurrent { get; }

	/// <summary>
	///     Applies damage and returns the updated health instance.
	/// </summary>
	/// <param name="amount">The amount of damage to apply.</param>
	/// <returns>The updated health.</returns>
	TSelf ApplyDamage(uint amount);
}
