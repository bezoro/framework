using System;
using System.Collections.Generic;
using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.DamageSystem.Services;

/// <summary>
///     Convenience entry point for applying damage to a damageable target.
/// </summary>
public static class DamageService
{
	/// <summary>
	///     Applies a simple damage amount with an unspecified type.
	/// </summary>
	public static DamageResult Apply<THealth>(IDamageable<THealth> target, int amount)
		where THealth : struct, IDamageableHealth<THealth>
		=> Apply(target, new DamageRequest(amount, DamageType.Unspecified));

	/// <summary>
	///     Applies a simple damage amount with an explicit type.
	/// </summary>
	public static DamageResult Apply<THealth>(IDamageable<THealth> target, float amount, DamageType type)
		where THealth : struct, IDamageableHealth<THealth>
		=> Apply(target, new DamageRequest(amount, type));

	/// <summary>
	///     Applies a damage request using the default resolver.
	/// </summary>
	public static DamageResult Apply<THealth>(IDamageable<THealth> target, in DamageRequest request)
		where THealth : struct, IDamageableHealth<THealth>
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));

		return DamageResolver<THealth>.Basic.Resolve(request, target);
	}

	/// <summary>
	///     Applies a damage request using a custom resolver.
	/// </summary>
	public static DamageResult Apply<THealth>(
		IDamageable<THealth> target,
		in DamageRequest     request,
		IDamageResolver<THealth> resolver)
		where THealth : struct, IDamageableHealth<THealth>
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));

		if (resolver is null)
			throw new ArgumentNullException(nameof(resolver));

		return resolver.Resolve(request, target);
	}

	/// <summary>
	///     Applies a multi-component damage request.
	/// </summary>
	public static DamageResult Apply<THealth>(
		IDamageable<THealth>        target,
		IReadOnlyList<DamageComponent> components,
		IDamageResolver<THealth>?   resolver = null)
		where THealth : struct, IDamageableHealth<THealth>
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));

		if (components is null)
			throw new ArgumentNullException(nameof(components));

		var request = DamageRequest.FromComponents(components);
		return resolver is null ? DamageResolver<THealth>.Basic.Resolve(request, target) : resolver.Resolve(request, target);
	}
}
