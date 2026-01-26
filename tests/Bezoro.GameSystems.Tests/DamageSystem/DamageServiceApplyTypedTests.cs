using System;
using Bezoro.GameSystems.DamageSystem.Services;
using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.DamageSystem;

[TestSubject(typeof(DamageService))]
public class DamageServiceApplyTypedTests
{
	[Fact]
	public void WhenTargetIsNull_ShouldThrow()
	{
		var act = () => DamageService.Apply(null!, 10f, DamageType.Fire);

		act.Should().Throw<ArgumentNullException>().WithParameterName("target");
	}

	[Fact]
	public void WhenValidTarget_ShouldApplySpecifiedTypeDamage()
	{
		var target = new TestDamageable(new Health(100u, 100u));

		var result = DamageService.Apply(target, 12f, DamageType.Fire);

		target.Health.Current.Should().Be(88u);
		result.Components.Should().HaveCount(1);
		result.Components[0].Type.Should().Be(DamageType.Fire);
		result.Components[0].Amount.Should().Be(12f);
	}
}
