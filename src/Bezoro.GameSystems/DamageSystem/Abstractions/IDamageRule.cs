using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.DamageSystem.Abstractions;

/// <summary>
///     Defines a mutation step applied during damage resolution.
/// </summary>
public interface IDamageRule<THealth>
	where THealth : struct, IDamageableHealth<THealth>
{
	/// <summary>
	///     Applies this rule to the provided damage context.
	/// </summary>
	void Apply(DamageContext<THealth> context);
}
