using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(PoolPolicy<>))]
public class PoolPolicyValidateTests
{
	[Fact]
	public void WithNoValidateDelegate_ShouldReturnTrue()
	{
		var policy = new PoolPolicy<object>(() => new());
		var item   = new object();

		bool result = policy.Validate(item);

		result.Should().BeTrue();
	}

	[Fact]
	public void WithValidateDelegate_ShouldInvokeDelegate()
	{
		var validateCalled = false;
		var policy = new PoolPolicy<object>(
			() => new(),
			validate: _ =>
			{
				validateCalled = true;
				return true;
			}
		);

		var item = new object();

		policy.Validate(item);

		validateCalled.Should().BeTrue();
	}

	[Fact]
	public void WithValidateDelegate_ShouldReturnDelegateResult()
	{
		var policy = new PoolPolicy<object>(
			() => new(),
			validate: _ => false
		);

		var item = new object();

		bool result = policy.Validate(item);

		result.Should().BeFalse();
	}
}
