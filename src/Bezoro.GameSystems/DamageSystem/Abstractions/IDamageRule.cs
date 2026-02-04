using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.DamageSystem.Abstractions;

/// <summary>
///     Defines a transformation step applied during damage resolution.
/// </summary>
public interface IDamageRule<THealth>
	where THealth : struct, IDamageableHealth<THealth>
{
	/// <summary>
	///     Applies this rule to the provided damage context.
	/// </summary>
	/// <param name="context">The incoming damage context.</param>
	/// <returns>The updated damage context.</returns>
	DamageContext<THealth> Apply(DamageContext<THealth> context);
}
