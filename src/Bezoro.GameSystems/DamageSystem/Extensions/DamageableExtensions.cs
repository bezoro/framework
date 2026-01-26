using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.DamageSystem.Core;
using Bezoro.GameSystems.DamageSystem.Types;

namespace Bezoro.GameSystems.DamageSystem.Extensions;

/// <summary>
///     Extension helpers for applying damage to damageable targets.
/// </summary>
public static class DamageableExtensions
{
	/// <summary>
	///     Applies a simple damage amount with an unspecified type.
	/// </summary>
	public static DamageResult ApplyDamage(this IDamageable target, int amount)
		=> Core.DamageSystem.Apply(target, amount);

	/// <summary>
	///     Applies a simple damage amount with an explicit type.
	/// </summary>
	public static DamageResult ApplyDamage(this IDamageable target, float amount, DamageType type)
		=> Core.DamageSystem.Apply(target, amount, type);

	/// <summary>
	///     Applies a damage request using the default resolver.
	/// </summary>
	public static DamageResult ApplyDamage(this IDamageable target, in DamageRequest request)
		=> Core.DamageSystem.Apply(target, request);

	/// <summary>
	///     Applies a damage request using a custom resolver.
	/// </summary>
	public static DamageResult ApplyDamage(this IDamageable target, in DamageRequest request, IDamageResolver resolver)
		=> Core.DamageSystem.Apply(target, request, resolver);
}
