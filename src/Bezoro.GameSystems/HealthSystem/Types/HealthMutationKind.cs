namespace Bezoro.GameSystems.HealthSystem.Types;

/// <summary>
///     Request type consumed by <see cref="Services.HealthSystem" />.
/// </summary>
public enum HealthMutationKind : byte
{
	/// <summary>
	///     Apply damage (consumes excess first, then base health).
	/// </summary>
	Damage = 0,

	/// <summary>
	///     Apply healing to base health only (does not overflow into excess).
	/// </summary>
	Heal = 1,

	/// <summary>
	///     Set max health.
	/// </summary>
	SetMax = 2,

	/// <summary>
	///     Apply damage directly to base health (ignores excess).
	/// </summary>
	DirectDamage = 3,

	/// <summary>
	///     Apply healing that overflows into excess health.
	/// </summary>
	IncreaseHealth = 4
}
