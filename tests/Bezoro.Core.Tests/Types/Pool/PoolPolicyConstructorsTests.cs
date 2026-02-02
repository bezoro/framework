using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(PoolPolicy<>))]
public class PoolPolicyConstructorsTests
{
	[Fact]
	public void WithAllDelegates_ShouldCreatePolicy()
	{
		var policy = new PoolPolicy<object>(
			() => new(),
			_ => true,
			_ => true,
			_ => { }
		);

		policy.Should().NotBeNull();
	}

	[Fact]
	public void WithFactory_ShouldCreatePolicy()
	{
		var policy = new PoolPolicy<object>(() => new());

		policy.Should().NotBeNull();
	}

	[Fact]
	public void WithNullFactory_ShouldThrow()
	{
		var act = () => new PoolPolicy<object>(null!);

		act.Should().Throw<ArgumentNullException>();
	}
}
