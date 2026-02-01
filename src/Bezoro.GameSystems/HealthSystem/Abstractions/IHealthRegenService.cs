using Bezoro.GameSystems.HealthSystem.Types;

namespace Bezoro.GameSystems.HealthSystem.Abstractions;

/// <summary>
///     Provides timed health-over-time regeneration effects by composing timer and health services.
/// </summary>
public interface IHealthRegenService
{
	/// <summary>
	///     Gets the number of currently active regeneration effects.
	/// </summary>
	int ActiveCount { get; }

	/// <summary>
	///     Gets whether a specific regen effect is currently active.
	/// </summary>
	/// <param name="handle">The regen handle to check.</param>
	/// <returns><c>true</c> if the regen is still ticking; <c>false</c> otherwise.</returns>
	bool IsActive(RegenHandle handle);

	/// <summary>
	///     Clears all existing regens on the target, then starts a new one.
	///     Heals <paramref name="amountPerSecond" /> HP each second for <paramref name="durationSeconds" /> seconds.
	/// </summary>
	/// <param name="target">The health instance to regenerate.</param>
	/// <param name="amountPerSecond">HP restored per second.</param>
	/// <param name="durationSeconds">Total duration in seconds.</param>
	/// <returns>A handle to the new regen effect.</returns>
	RegenHandle StartRegen(IHealth target, uint amountPerSecond, float durationSeconds);

	/// <summary>
	///     Clears all existing regens on the target, then starts a new one.
	///     Distributes <paramref name="totalAmount" /> HP evenly across <paramref name="ticks" /> applications.
	/// </summary>
	/// <param name="target">The health instance to regenerate.</param>
	/// <param name="totalAmount">Total HP to restore over the full duration.</param>
	/// <param name="ticks">Number of discrete heal applications.</param>
	/// <returns>A handle to the new regen effect.</returns>
	RegenHandle StartRegen(IHealth target, uint totalAmount, uint ticks);

	/// <summary>
	///     Stacks a new regen alongside any existing ones on the target.
	///     Heals <paramref name="amountPerSecond" /> HP each second for <paramref name="durationSeconds" /> seconds.
	/// </summary>
	/// <param name="target">The health instance to regenerate.</param>
	/// <param name="amountPerSecond">HP restored per second.</param>
	/// <param name="durationSeconds">Total duration in seconds.</param>
	/// <returns>A handle to the new regen effect.</returns>
	RegenHandle AddRegen(IHealth target, uint amountPerSecond, float durationSeconds);

	/// <summary>
	///     Stacks a new regen alongside any existing ones on the target.
	///     Distributes <paramref name="totalAmount" /> HP evenly across <paramref name="ticks" /> applications.
	/// </summary>
	/// <param name="target">The health instance to regenerate.</param>
	/// <param name="totalAmount">Total HP to restore over the full duration.</param>
	/// <param name="ticks">Number of discrete heal applications.</param>
	/// <returns>A handle to the new regen effect.</returns>
	RegenHandle AddRegen(IHealth target, uint totalAmount, uint ticks);

	/// <summary>
	///     Cancels a specific regen effect.
	/// </summary>
	/// <param name="handle">The regen to cancel.</param>
	/// <returns><c>true</c> if the regen was active and is now stopped; <c>false</c> otherwise.</returns>
	bool Stop(RegenHandle handle);

	/// <summary>
	///     Cancels all regen effects on the specified target.
	/// </summary>
	/// <param name="target">The health instance to stop all regens on.</param>
	void StopAll(IHealth target);
}
