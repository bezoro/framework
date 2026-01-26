namespace Bezoro.GameSystems.DamageSystem.Abstractions;

/// <summary>
///     Represents a damage source (attacker, ability, projectile, trap, etc.).
/// </summary>
public interface IDamageSource
{
	object? Source { get; }
}
