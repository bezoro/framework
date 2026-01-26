using System;
using Bezoro.GameSystems.DamageSystem.Services;
using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.DamageSystem;

[TestSubject(typeof(DamageService))]
public class DamageServiceApplySimpleTests
{
	[Fact]
	public void WhenTargetIsNull_ShouldThrow()
	{
		var act = () => DamageService.Apply(null!, 10);

		act.Should().Throw<ArgumentNullException>().WithParameterName("target");
	}

	[Fact]
	public void WhenValidTarget_ShouldApplyUnspecifiedDamage()
	{
		var target = new TestDamageable(new Health(100u, 100u));

		var result = DamageService.Apply(target, 15);

		target.Health.Current.Should().Be(85u);
		result.IntendedDamage.Should().Be(15u);
		result.AppliedDamage.Should().Be(15u);
		result.WasCancelled.Should().BeFalse();
		result.Components.Should().HaveCount(1);
		result.Components[0].Type.Should().Be(DamageType.Unspecified);
		result.Components[0].Amount.Should().Be(15f);
	}
}
