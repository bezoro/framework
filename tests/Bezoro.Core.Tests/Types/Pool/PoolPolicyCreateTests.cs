using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(PoolPolicy<>))]
public class PoolPolicyCreateTests
{
	[Fact]
	public void ShouldInvokeFactory()
	{
		var invoked = false;
		var policy = new PoolPolicy<object>(() =>
			{
				invoked = true;
				return new();
			}
		);

		object item = policy.Create();

		invoked.Should().BeTrue();
		item.Should().NotBeNull();
	}

	[Fact]
	public void ShouldReturnNewInstanceEachTime()
	{
		var policy = new PoolPolicy<object>(() => new());

		object item1 = policy.Create();
		object item2 = policy.Create();

		item1.Should().NotBeSameAs(item2);
	}
}
