using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.Tests.DamageSystem;

internal sealed class TestDamageable(IHealth health) : IDamageable
{
	public IHealth Health { get; } = health;
}
