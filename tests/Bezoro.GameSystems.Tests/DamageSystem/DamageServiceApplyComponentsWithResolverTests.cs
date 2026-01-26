using System;
using System.Collections.Generic;
using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.DamageSystem.Services;
using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;
using Xunit;

namespace Bezoro.GameSystems.Tests.DamageSystem;

[TestSubject(typeof(DamageService))]
public class DamageServiceApplyComponentsWithResolverTests
{
	[Fact]
	public void WhenCustomResolverProvided_ShouldForwardComponents()
	{
		var target = new TestDamageable(new Health(100u, 100u));
		var components = new List<DamageComponent>
		{
			new(DamageType.Lightning, 3f),
			new(DamageType.Poison, 2f)
		};

		var resolver = Substitute.For<IDamageResolver>();
		var expected = new DamageResult(
			100u,
			100u,
			0u,
			0u,
			0f,
			Array.Empty<DamageComponent>(),
			false);

		DamageRequest? forwarded = null;

		resolver.Resolve(Arg.Any<DamageRequest>(), Arg.Any<IDamageable>())
				.Returns(callInfo =>
				{
					forwarded = callInfo.ArgAt<DamageRequest>(0);
					return expected;
				});

		var result = DamageService.Apply(target, components, resolver);

		target.Health.Current.Should().Be(100u);
		result.IntendedDamage.Should().Be(expected.IntendedDamage);
		result.Components.Should().Equal(expected.Components);
		resolver.Received(1).Resolve(Arg.Any<DamageRequest>(), target);
		forwarded.HasValue.Should().BeTrue();
		forwarded!.Value.HasComponents.Should().BeTrue();
		forwarded.Value.Components.Should().Equal(components);
		forwarded.Value.BaseAmount.Should().Be(0f);
		forwarded.Value.Type.Should().Be(DamageType.Unspecified);
	}
}
