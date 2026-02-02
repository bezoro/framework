using System;
using Bezoro.GameSystems.HealthSystem.Types;

namespace Bezoro.GameSystems.HealthSystem.Abstractions;

/// <summary>
///     Provides timed health-over-time regeneration effects by composing timer and health services.
/// </summary>
public interface IHealthRegenService : IDisposable
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
	///     Clears all existing regens on the target, then starts a finite regen.
	///     Delivers exactly <c>Round(amountPerSec * duration.TotalSeconds)</c> total HP over the duration.
	/// </summary>
	/// <param name="target">The health instance to regenerate.</param>
	/// <param name="amountPerSec">HP restored per second. Must be positive and finite.</param>
	/// <param name="duration">Total duration. Must be positive.</param>
	/// <param name="tickFrequencyMs">Milliseconds between ticks (default 20 = 50 ticks/sec).</param>
	/// <returns>A handle to the new regen effect.</returns>
	RegenHandle StartRegen(IHealth target, float amountPerSec, TimeSpan duration, uint tickFrequencyMs = 20);

	/// <summary>
	///     Stacks a finite regen alongside any existing ones on the target.
	///     Delivers exactly <c>Round(amountPerSec * duration.TotalSeconds)</c> total HP over the duration.
	/// </summary>
	/// <param name="target">The health instance to regenerate.</param>
	/// <param name="amountPerSec">HP restored per second. Must be positive and finite.</param>
	/// <param name="duration">Total duration. Must be positive.</param>
	/// <param name="tickFrequencyMs">Milliseconds between ticks (default 20 = 50 ticks/sec).</param>
	/// <returns>A handle to the new regen effect.</returns>
	RegenHandle AddRegen(IHealth target, float amountPerSec, TimeSpan duration, uint tickFrequencyMs = 20);

	/// <summary>
	///     Clears all existing regens on the target, then starts an infinite regen.
	///     Delivers <paramref name="amountPerSec" /> HP/s until explicitly stopped.
	/// </summary>
	/// <param name="target">The health instance to regenerate.</param>
	/// <param name="amountPerSec">HP restored per second. Must be positive and finite.</param>
	/// <param name="tickFrequencyMs">Milliseconds between ticks (default 20 = 50 ticks/sec).</param>
	/// <returns>A handle to the new regen effect.</returns>
	RegenHandle StartRepeatingRegen(IHealth target, float amountPerSec, uint tickFrequencyMs = 20);

	/// <summary>
	///     Stacks an infinite regen alongside any existing ones on the target.
	///     Delivers <paramref name="amountPerSec" /> HP/s until explicitly stopped.
	/// </summary>
	/// <param name="target">The health instance to regenerate.</param>
	/// <param name="amountPerSec">HP restored per second. Must be positive and finite.</param>
	/// <param name="tickFrequencyMs">Milliseconds between ticks (default 20 = 50 ticks/sec).</param>
	/// <returns>A handle to the new regen effect.</returns>
	RegenHandle AddRepeatingRegen(IHealth target, float amountPerSec, uint tickFrequencyMs = 20);

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

	/// <summary>
	///     Advances all active regens by the given delta time.
	///     In production, this is called automatically by the internal timer.
	///     Exposed for deterministic testing.
	/// </summary>
	/// <param name="deltaTime">Elapsed time in seconds.</param>
	void Update(float deltaTime);
}
