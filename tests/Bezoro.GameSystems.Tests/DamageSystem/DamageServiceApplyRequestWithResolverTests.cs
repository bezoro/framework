using System;
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
public class DamageServiceApplyRequestWithResolverTests
{
	[Fact]
	public void WhenCustomResolverProvided_ShouldForwardRequestAndReturnResult()
	{
		var target = new TestDamageable(new Health(100u, 100u));
		var request = new DamageRequest(
			7f,
			DamageType.Magic,
			2f,
			1f,
			DamageFlags.Critical);

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

		var result = DamageService.Apply(target, request, resolver);

		target.Health.Current.Should().Be(100u);
		result.IntendedDamage.Should().Be(expected.IntendedDamage);
		result.AppliedDamage.Should().Be(expected.AppliedDamage);
		result.RawDamage.Should().Be(expected.RawDamage);
		result.WasCancelled.Should().Be(expected.WasCancelled);
		result.Components.Should().Equal(expected.Components);
		resolver.Received(1).Resolve(Arg.Any<DamageRequest>(), target);
		forwarded.HasValue.Should().BeTrue();
		forwarded!.Value.BaseAmount.Should().Be(7f);
		forwarded.Value.Type.Should().Be(DamageType.Magic);
		forwarded.Value.Multiplier.Should().Be(2f);
		forwarded.Value.FlatBonus.Should().Be(1f);
		forwarded.Value.Flags.Should().Be(DamageFlags.Critical);
	}

	[Fact]
	public void WhenResolverIsNull_ShouldThrow()
	{
		var target = new TestDamageable(new Health(100u, 100u));
		var act    = () => DamageService.Apply(target, new DamageRequest(1f, DamageType.Fire), null!);

		act.Should().Throw<ArgumentNullException>().WithParameterName("resolver");
	}

	[Fact]
	public void WhenTargetIsNull_ShouldThrow()
	{
		var resolver = Substitute.For<IDamageResolver>();
		var act      = () => DamageService.Apply(null!, new DamageRequest(1f, DamageType.Fire), resolver);

		act.Should().Throw<ArgumentNullException>().WithParameterName("target");
	}
}
