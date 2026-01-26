using System;
using System.Collections.Generic;
using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.DamageSystem.Services;
using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Abstractions;
using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;
using Xunit;

namespace Bezoro.GameSystems.Tests.DamageSystem;

[TestSubject(typeof(DamageService))]
public static class DamageServiceTests
{
	public class DefaultResolverProperty
	{
		[Fact]
		public void ShouldExposeBasicResolver()
		{
			DamageService.DefaultResolver.Should().BeSameAs(DamageResolver.Basic);
		}
	}

	public class ApplySimple
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

	public class ApplyTyped
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

	public class ApplyRequest
	{
		[Fact]
		public void WhenTargetIsNull_ShouldThrow()
		{
			var act = () => DamageService.Apply(null!, new DamageRequest(10f, DamageType.Physical));

			act.Should().Throw<ArgumentNullException>().WithParameterName("target");
		}

		[Fact]
		public void WhenUsingDefaultResolver_ShouldMatchDefaultResolver()
		{
			var request = new DamageRequest(15f, DamageType.Physical, multiplier: 1.5f, flatBonus: 2f);
			var serviceTarget = new TestDamageable(new Health(100u, 100u));
			var resolverTarget = new TestDamageable(new Health(100u, 100u));

			var serviceResult = DamageService.Apply(serviceTarget, request);
			var resolverResult = DamageService.DefaultResolver.Resolve(request, resolverTarget);

			serviceTarget.Health.Current.Should().Be(resolverTarget.Health.Current);
			serviceResult.HealthBefore.Should().Be(resolverResult.HealthBefore);
			serviceResult.HealthAfter.Should().Be(resolverResult.HealthAfter);
			serviceResult.IntendedDamage.Should().Be(resolverResult.IntendedDamage);
			serviceResult.AppliedDamage.Should().Be(resolverResult.AppliedDamage);
			serviceResult.RawDamage.Should().Be(resolverResult.RawDamage);
			serviceResult.WasCancelled.Should().Be(resolverResult.WasCancelled);
			serviceResult.Components.Should().Equal(resolverResult.Components);
		}
	}

	public class ApplyWithResolver
	{
		[Fact]
		public void WhenTargetIsNull_ShouldThrow()
		{
			var resolver = Substitute.For<IDamageResolver>();
			var act = () => DamageService.Apply(null!, new DamageRequest(1f, DamageType.Fire), resolver);

			act.Should().Throw<ArgumentNullException>().WithParameterName("target");
		}

		[Fact]
		public void WhenResolverIsNull_ShouldThrow()
		{
			var target = new TestDamageable(new Health(100u, 100u));
			var act = () => DamageService.Apply(target, new DamageRequest(1f, DamageType.Fire), null!);

			act.Should().Throw<ArgumentNullException>().WithParameterName("resolver");
		}

		[Fact]
		public void WhenCustomResolverProvided_ShouldForwardRequestAndReturnResult()
		{
			var target = new TestDamageable(new Health(100u, 100u));
			var request = new DamageRequest(
				7f,
				DamageType.Magic,
				multiplier: 2f,
				flatBonus: 1f,
				flags: DamageFlags.Critical);
			var resolver = Substitute.For<IDamageResolver>();
			var expected = new DamageResult(
				healthBefore: 100u,
				healthAfter: 100u,
				intendedDamage: 0u,
				appliedDamage: 0u,
				rawDamage: 0f,
				components: Array.Empty<DamageComponent>(),
				wasCancelled: false);
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
	}

	public class ApplyComponents
	{
		[Fact]
		public void WhenTargetIsNull_ShouldThrow()
		{
			var act = () => DamageService.Apply(null!, Array.Empty<DamageComponent>());

			act.Should().Throw<ArgumentNullException>().WithParameterName("target");
		}

		[Fact]
		public void WhenComponentsIsNull_ShouldThrow()
		{
			var target = new TestDamageable(new Health(100u, 100u));
			var act = () => DamageService.Apply(target, null!);

			act.Should().Throw<ArgumentNullException>().WithParameterName("components");
		}

		[Fact]
		public void WhenResolverIsNull_ShouldApplyComponentsUsingDefaultResolver()
		{
			var target = new TestDamageable(new Health(100u, 100u));
			var components = new[]
			{
				new DamageComponent(DamageType.Fire, 7f),
				new DamageComponent(DamageType.Ice, 8f)
			};

			var result = DamageService.Apply(target, components);

			target.Health.Current.Should().Be(85u);
			result.IntendedDamage.Should().Be(15u);
			result.Components.Should().Equal(components);
		}
	}

	public class ApplyComponentsWithResolver
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
				healthBefore: 100u,
				healthAfter: 100u,
				intendedDamage: 0u,
				appliedDamage: 0u,
				rawDamage: 0f,
				components: Array.Empty<DamageComponent>(),
				wasCancelled: false);
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

	private sealed class TestDamageable : IDamageable
	{
		public TestDamageable(IHealth health)
		{
			Health = health;
		}

		public IHealth Health { get; }
	}
}
