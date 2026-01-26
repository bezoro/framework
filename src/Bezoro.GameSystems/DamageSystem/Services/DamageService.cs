using System;
using System.Collections.Generic;
using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.DamageSystem.Types;

namespace Bezoro.GameSystems.DamageSystem.Services;

/// <summary>
///     Convenience entry point for applying damage to a damageable target.
/// </summary>
public static class DamageService
{
	/// <summary>
	///     Gets the default resolver used when no resolver is supplied.
	/// </summary>
	public static DamageResolver DefaultResolver { get; } = DamageResolver.Basic;

	/// <summary>
	///     Applies a simple damage amount with an unspecified type.
	/// </summary>
	public static DamageResult Apply(IDamageable target, int amount)
		=> Apply(target, new DamageRequest(amount, DamageType.Unspecified));

	/// <summary>
	///     Applies a simple damage amount with an explicit type.
	/// </summary>
	public static DamageResult Apply(IDamageable target, float amount, DamageType type)
		=> Apply(target, new DamageRequest(amount, type));

	/// <summary>
	///     Applies a damage request using the default resolver.
	/// </summary>
	public static DamageResult Apply(IDamageable target, in DamageRequest request)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));

		return DefaultResolver.Resolve(request, target);
	}

	/// <summary>
	///     Applies a damage request using a custom resolver.
	/// </summary>
	public static DamageResult Apply(IDamageable target, in DamageRequest request, IDamageResolver resolver)
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
	public static DamageResult Apply(
		IDamageable                    target,
		IReadOnlyList<DamageComponent> components,
		IDamageResolver?               resolver = null)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));

		if (components is null)
			throw new ArgumentNullException(nameof(components));

		var request = DamageRequest.FromComponents(components);
		return resolver is null ? DefaultResolver.Resolve(request, target) : resolver.Resolve(request, target);
	}
}
