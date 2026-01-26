using System.Collections.Generic;
using Bezoro.GameSystems.DamageSystem.Abstractions;

namespace Bezoro.GameSystems.DamageSystem.Types;

/// <summary>
///     Configuration options for the damage resolver.
/// </summary>
public readonly struct DamageResolverConfig(
	IReadOnlyList<IDamageRule>? rules                = null,
	DamageRoundingMode          roundingMode         = DamageRoundingMode.RoundToNearest,
	uint                        minimumAppliedDamage = 0,
	uint?                       maximumAppliedDamage = null,
	bool                        clampToCurrentHealth = true
)
{
	/// <summary>
	///     Gets whether the intended damage should be clamped to the current health.
	/// </summary>
	public readonly bool ClampToCurrentHealth = clampToCurrentHealth;

	/// <summary>
	///     Gets the rounding mode used when converting raw damage to an integer.
	/// </summary>
	public readonly DamageRoundingMode RoundingMode = roundingMode;
	/// <summary>
	///     Gets the rules applied during resolution in order.
	/// </summary>
	public readonly IReadOnlyList<IDamageRule>? Rules = rules;

	/// <summary>
	///     Gets the minimum applied damage after rounding.
	/// </summary>
	public readonly uint MinimumAppliedDamage = minimumAppliedDamage;

	/// <summary>
	///     Gets the optional maximum applied damage after rounding.
	/// </summary>
	public readonly uint? MaximumAppliedDamage = maximumAppliedDamage;
}
