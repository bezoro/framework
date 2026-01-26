using System.Collections.Generic;
using Bezoro.GameSystems.DamageSystem.Types;

namespace Bezoro.GameSystems.DamageSystem.Core;

/// <summary>
///     Result of applying a damage request.
/// </summary>
public readonly struct DamageResult(
	uint                           healthBefore,
	uint                           healthAfter,
	uint                           intendedDamage,
	uint                           appliedDamage,
	float                          rawDamage,
	IReadOnlyList<DamageComponent> components,
	bool                           wasCancelled
)
{
	/// <summary>
	///     Gets whether the damage was cancelled by a rule.
	/// </summary>
	public readonly bool WasCancelled = wasCancelled;

	/// <summary>
	///     Gets the raw damage before rounding and clamping.
	/// </summary>
	public readonly float RawDamage = rawDamage;

	/// <summary>
	///     Gets the adjusted components after rules.
	/// </summary>
	public readonly IReadOnlyList<DamageComponent> Components = components;

	/// <summary>
	///     Gets the actual applied damage (based on health delta).
	/// </summary>
	public readonly uint AppliedDamage = appliedDamage;

	/// <summary>
	///     Gets the health value after damage was applied.
	/// </summary>
	public readonly uint HealthAfter = healthAfter;
	/// <summary>
	///     Gets the health value before damage was applied.
	/// </summary>
	public readonly uint HealthBefore = healthBefore;

	/// <summary>
	///     Gets the intended damage after rules and rounding.
	/// </summary>
	public readonly uint IntendedDamage = intendedDamage;

	/// <summary>
	///     Gets whether the target was reduced to zero or below.
	/// </summary>
	public bool WasFatal => HealthAfter == 0;

	/// <summary>
	///     Gets the amount of intended damage that did not apply.
	/// </summary>
	public uint Overkill => IntendedDamage > AppliedDamage ? IntendedDamage - AppliedDamage : 0;
}
