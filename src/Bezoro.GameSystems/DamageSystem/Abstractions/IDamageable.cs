using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.DamageSystem.Abstractions;

/// <summary>
///     Represents a damageable target with health.
/// </summary>
/// <typeparam name="THealth">The health type.</typeparam>
/// <remarks>
///     Implementations must ensure <see cref="TryUpdateHealth" /> is atomic and thread-safe.
/// </remarks>
public interface IDamageable<THealth>
	where THealth : struct, IDamageableHealth<THealth>
{
	/// <summary>
	///     Gets a snapshot of the current health.
	/// </summary>
	THealth Health { get; }

	/// <summary>
	///     Attempts to atomically update health from an expected value to a new value.
	/// </summary>
	/// <param name="expected">The expected current health.</param>
	/// <param name="updated">The new health value.</param>
	/// <returns><c>true</c> if the update was applied; otherwise <c>false</c>.</returns>
	bool TryUpdateHealth(THealth expected, THealth updated);
}
