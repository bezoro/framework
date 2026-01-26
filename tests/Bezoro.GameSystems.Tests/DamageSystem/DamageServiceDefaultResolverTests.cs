using Bezoro.GameSystems.DamageSystem.Services;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.DamageSystem;

[TestSubject(typeof(DamageService))]
public class DamageServiceDefaultResolverTests
{
	[Fact]
	public void ShouldExposeBasicResolver()
	{
		DamageService.DefaultResolver.Should().BeSameAs(DamageResolver.Basic);
	}
}
