using Bezoro.GameSystems.DamageSystem.Services;
using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.DamageSystem;

[TestSubject(typeof(DamageResolver<>))]
public class DamageResolverExcessTests
{
	[Fact]
	public void Resolve_WhenTargetHasExcess_ShouldConsumeExcessFirst()
	{
		var health = new HealthWithExcess(max: 100u, current: 100u, excess: 20u, excessMax: 50u);
		var target = new TestDamageable<HealthWithExcess>(health);

		var result = DamageResolver<HealthWithExcess>.Basic.Resolve(DamageRequest.Simple(10f), target);

		target.Health.Current.Should().Be(100u);
		target.Health.ExcessCurrent.Should().Be(10u);
		result.AppliedDamage.Should().Be(10u);
	}
}
