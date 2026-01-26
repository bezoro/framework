using System;
using Bezoro.GameSystems.DamageSystem.Services;
using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.DamageSystem;

[TestSubject(typeof(DamageService))]
public class DamageServiceApplyRequestTests
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
		var request        = new DamageRequest(15f, DamageType.Physical, 1.5f, 2f);
		var serviceTarget  = new TestDamageable(new Health(100u, 100u));
		var resolverTarget = new TestDamageable(new Health(100u, 100u));

		var serviceResult  = DamageService.Apply(serviceTarget, request);
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
