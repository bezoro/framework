using System;
using Bezoro.GameSystems.DamageSystem.Services;
using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.DamageSystem;

[TestSubject(typeof(DamageService))]
public class DamageServiceApplyComponentsTests
{
	[Fact]
	public void WhenComponentsIsNull_ShouldThrow()
	{
		var target = new TestDamageable<HealthWithExcess>(new(100u, 100u));
		var act    = () => DamageService.Apply(target, null!);

		act.Should().Throw<ArgumentNullException>().WithParameterName("components");
	}

	[Fact]
	public void WhenResolverIsNull_ShouldApplyComponentsUsingDefaultResolver()
	{
		var target = new TestDamageable<HealthWithExcess>(new(100u, 100u));
		var components = new[]
		{
			new DamageComponent(DamageType.Fire, 7f),
			new DamageComponent(DamageType.Ice,  8f)
		};

		var result = DamageService.Apply(target, components);

		target.Health.Current.Should().Be(85u);
		result.IntendedDamage.Should().Be(15u);
		result.Components.Should().Equal(components);
	}

	[Fact]
	public void WhenTargetIsNull_ShouldThrow()
	{
		var act = () => DamageService.Apply<HealthWithExcess>(null!, Array.Empty<DamageComponent>());

		act.Should().Throw<ArgumentNullException>().WithParameterName("target");
	}
}
