using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(PoolPolicy<>))]
public class PoolPolicyResetTests
{
	[Fact]
	public void WithIPooledObject_ShouldCallOnReturn()
	{
		var policy = new PoolPolicy<TestPooledObject>(() => new());
		var item   = new TestPooledObject();

		policy.Reset(item);

		item.OnReturnCount.Should().Be(1);
	}

	[Fact]
	public void WithIPooledObject_ShouldPreferOnReturnOverDelegate()
	{
		var delegateCalled = false;
		var policy = new PoolPolicy<TestPooledObject>(
			() => new(),
			_ =>
			{
				delegateCalled = true;
				return true;
			});

		var item = new TestPooledObject();

		policy.Reset(item);

		delegateCalled.Should().BeFalse();
		item.OnReturnCount.Should().Be(1);
	}

	[Fact]
	public void WithIPooledObject_ShouldReturnOnReturnResult()
	{
		var policy = new PoolPolicy<TestPooledObject>(() => new());
		var item   = new TestPooledObject { ReturnValue = false };

		bool result = policy.Reset(item);

		result.Should().BeFalse();
	}

	[Fact]
	public void WithNoResetDelegate_ShouldReturnTrue()
	{
		var policy = new PoolPolicy<object>(() => new());
		var item   = new object();

		bool result = policy.Reset(item);

		result.Should().BeTrue();
	}

	[Fact]
	public void WithResetDelegate_ShouldInvokeDelegate()
	{
		var resetCalled = false;
		var policy = new PoolPolicy<object>(
			() => new(),
			_ =>
			{
				resetCalled = true;
				return true;
			});

		var item = new object();

		policy.Reset(item);

		resetCalled.Should().BeTrue();
	}
}
