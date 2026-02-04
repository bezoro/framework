using Bezoro.GameSystems.DamageSystem.Services;
using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.DamageSystem;

[TestSubject(typeof(DamageResolver<>))]
public class DamageResolverBasicTests
{
	[Fact]
	public void ShouldExposeBasicResolverPerHealthType()
	{
		var healthResolver = DamageResolver<Health>.Basic;
		var excessResolver = DamageResolver<HealthWithExcess>.Basic;

		healthResolver.Should().NotBeNull();
		excessResolver.Should().NotBeNull();
		healthResolver.Should().NotBeSameAs(excessResolver);
	}
}
