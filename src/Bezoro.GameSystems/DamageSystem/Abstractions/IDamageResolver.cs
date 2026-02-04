using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.DamageSystem.Abstractions;

/// <summary>
///     Resolves damage requests into applied health changes.
/// </summary>
public interface IDamageResolver<THealth>
	where THealth : struct, IDamageableHealth<THealth>
{
	/// <summary>
	///     Resolves and applies the damage to the target.
	/// </summary>
	/// <param name="request">The damage request.</param>
	/// <param name="target">The health target.</param>
	/// <returns>A result describing the applied damage.</returns>
	DamageResult Resolve(in DamageRequest request, IDamageable<THealth> target);
}
