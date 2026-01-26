using Bezoro.GameSystems.DamageSystem.Core;

namespace Bezoro.GameSystems.DamageSystem.Abstractions;

/// <summary>
///     Defines a mutation step applied during damage resolution.
/// </summary>
public interface IDamageRule
{
	/// <summary>
	///     Applies this rule to the provided damage context.
	/// </summary>
	void Apply(DamageContext context);
}
