using System.Collections.Generic;
using Bezoro.GameSystems.DamageSystem.Abstractions;

namespace Bezoro.GameSystems.DamageSystem.Types;

/// <summary>
///     Describes an incoming damage request before mitigation.
/// </summary>
public readonly struct DamageRequest(
	float                           baseAmount,
	DamageType                      type,
	float                           multiplier = 1f,
	float                           flatBonus  = 0f,
	DamageFlags                     flags      = DamageFlags.None,
	IDamageSource?                  source     = null,
	IDamageable?                    target     = null,
	IReadOnlyList<DamageComponent>? components = null
)
{
	/// <summary>
	///     Gets optional flags that influence damage behavior.
	/// </summary>
	public readonly DamageFlags Flags = flags;

	/// <summary>
	///     Gets the damage type used when no components are supplied.
	/// </summary>
	public readonly DamageType Type = type;
	/// <summary>
	///     Gets the base amount used when no components are supplied.
	/// </summary>
	public readonly float BaseAmount = baseAmount;

	/// <summary>
	///     Gets a flat bonus added to the total after rules.
	/// </summary>
	public readonly float FlatBonus = flatBonus;

	/// <summary>
	///     Gets a multiplier applied to the total after rules and flat bonuses.
	/// </summary>
	public readonly float Multiplier = multiplier;

	/// <summary>
	///     Gets the optional target of the damage (defender, hitbox).
	///     Use <see cref="IDamageable.DamageContext" /> for extra metadata.
	/// </summary>
	public readonly IDamageable? Target = target;

	/// <summary>
	///     Gets the optional source of the damage (attacker, skill, projectile).
	///     Use <see cref="IDamageSource.DamageContext" /> for extra metadata.
	/// </summary>
	public readonly IDamageSource? Source = source;

	/// <summary>
	///     Gets optional damage components. When present, <see cref="BaseAmount" /> and <see cref="Type" /> are ignored.
	/// </summary>
	public readonly IReadOnlyList<DamageComponent>? Components = components;

	/// <summary>
	///     Gets whether this request supplies explicit components.
	/// </summary>
	public bool HasComponents => Components is { Count: > 0 };

	/// <summary>
	///     Creates a multi-component damage request.
	/// </summary>
	public static DamageRequest FromComponents(
		IReadOnlyList<DamageComponent> components,
		float                          multiplier = 1f,
		float                          flatBonus  = 0f,
		DamageFlags                    flags      = DamageFlags.None,
		IDamageSource?                 source     = null,
		IDamageable?                   target     = null)
		=> new(0f, DamageType.Unspecified, multiplier, flatBonus, flags, source, target, components);

	/// <summary>
	///     Creates a simple single-component damage request.
	/// </summary>
	public static DamageRequest Simple(float amount) => new(amount, DamageType.Unspecified);

	/// <summary>
	///     Creates a simple single-component damage request with an explicit type.
	/// </summary>
	public static DamageRequest Simple(float amount, DamageType type) => new(amount, type);
}
