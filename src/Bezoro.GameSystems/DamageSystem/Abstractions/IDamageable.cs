using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.DamageSystem.Abstractions;

/// <summary>
///     Represents a damageable target with health.
/// </summary>
public interface IDamageable
{
	/// <summary>
	///     Gets the health associated with this participant.
	/// </summary>
	IHealth Health { get; }
}
