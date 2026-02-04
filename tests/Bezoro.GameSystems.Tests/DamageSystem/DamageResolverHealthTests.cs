using Bezoro.GameSystems.DamageSystem.Services;
using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.DamageSystem;

[TestSubject(typeof(DamageResolver<>))]
public class DamageResolverHealthTests
{
	[Fact]
	public void Resolve_WhenUsingHealth_ShouldApplyDamageToCurrent()
	{
		var health = new Health(max: 50u, current: 40u);
		var target = new TestDamageable<Health>(health);

		var result = DamageResolver<Health>.Basic.Resolve(DamageRequest.Simple(10f), target);

		target.Health.Current.Should().Be(30u);
		result.HealthBefore.Should().Be(40u);
		result.HealthAfter.Should().Be(30u);
		result.AppliedDamage.Should().Be(10u);
	}
}
