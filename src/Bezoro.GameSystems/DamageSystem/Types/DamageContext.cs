using System;
using System.Collections.Generic;
using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.DamageSystem.Types;

/// <summary>
///     Immutable context passed through damage rules.
/// </summary>
/// <remarks>
///     Thread-safe by design; mutations return a new context instance.
/// </remarks>
public readonly record struct DamageContext<THealth>
	where THealth : struct, IDamageableHealth<THealth>
{
	internal DamageContext(
		DamageRequest                 request,
		IDamageable<THealth>          target,
		uint                          healthBefore,
		IReadOnlyList<DamageComponent> components)
	{
		Request          = request;
		Target           = target ?? throw new ArgumentNullException(nameof(target), "Target cannot be null.");
		HealthBefore     = healthBefore;
		Components       = components ?? throw new ArgumentNullException(nameof(components), "Components cannot be null.");
		GlobalMultiplier = 1f;
	}

	/// <summary>
	///     Gets the original damage request.
	/// </summary>
	public DamageRequest Request { get; }

	/// <summary>
	///     Gets the damageable target receiving damage.
	/// </summary>
	public IDamageable<THealth> Target { get; }

	/// <summary>
	///     Gets the damage components snapshot.
	/// </summary>
	public IReadOnlyList<DamageComponent> Components { get; init; }

	/// <summary>
	///     Gets the effective health value before damage was applied.
	/// </summary>
	public uint HealthBefore { get; }

	/// <summary>
	///     Gets whether the damage has been cancelled by a rule.
	/// </summary>
	public bool IsCancelled { get; init; }

	/// <summary>
	///     Gets a flat bonus added to the total.
	/// </summary>
	public float GlobalFlatBonus { get; init; }

	/// <summary>
	///     Gets a multiplier applied to the total.
	/// </summary>
	public float GlobalMultiplier { get; init; }

	/// <summary>
	///     Adds to the global flat bonus and returns the updated context.
	/// </summary>
	public DamageContext<THealth> AddFlatBonus(float flatBonus) =>
		this with { GlobalFlatBonus = GlobalFlatBonus + flatBonus };

	/// <summary>
	///     Cancels the damage, resulting in zero applied damage, and returns the updated context.
	/// </summary>
	public DamageContext<THealth> Cancel() => this with { IsCancelled = true };

	/// <summary>
	///     Multiplies the current global multiplier and returns the updated context.
	/// </summary>
	public DamageContext<THealth> MultiplyAll(float multiplier) =>
		this with { GlobalMultiplier = GlobalMultiplier * multiplier };
}
