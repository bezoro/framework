using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.DamageSystem.Services;
using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.DamageSystem.Extensions;

/// <summary>
///     Extension helpers for applying damage to damageable targets.
/// </summary>
public static class DamageableExtensions
{
	/// <summary>
	///     Applies a simple damage amount with an unspecified type.
	/// </summary>
	public static DamageResult ApplyDamage<THealth>(this IDamageable<THealth> target, int amount)
		where THealth : struct, IDamageableHealth<THealth>
		=> DamageService.Apply(target, amount);

	/// <summary>
	///     Applies a simple damage amount with an explicit type.
	/// </summary>
	public static DamageResult ApplyDamage<THealth>(this IDamageable<THealth> target, float amount, DamageType type)
		where THealth : struct, IDamageableHealth<THealth>
		=> DamageService.Apply(target, amount, type);

	/// <summary>
	///     Applies a damage request using the default resolver.
	/// </summary>
	public static DamageResult ApplyDamage<THealth>(this IDamageable<THealth> target, in DamageRequest request)
		where THealth : struct, IDamageableHealth<THealth>
		=> DamageService.Apply(target, request);

	/// <summary>
	///     Applies a damage request using a custom resolver.
	/// </summary>
	public static DamageResult ApplyDamage<THealth>(
		this IDamageable<THealth> target,
		in DamageRequest          request,
		IDamageResolver<THealth>  resolver)
		where THealth : struct, IDamageableHealth<THealth>
		=> DamageService.Apply(target, request, resolver);
}
