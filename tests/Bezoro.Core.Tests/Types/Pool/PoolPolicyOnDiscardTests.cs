using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(PoolPolicy<>))]
public class PoolPolicyOnDiscardTests
{
	[Fact]
	public void WithDiscardDelegateAndDisposable_WhenCalled_ShouldCallBoth()
	{
		var discardCalled = false;
		var policy = new PoolPolicy<DisposableTestObject>(
			() => new(),
			onDiscard: _ => discardCalled = true
		);

		var item = new DisposableTestObject();

		policy.OnDiscard(item);

		discardCalled.Should().BeTrue();
		item.IsDisposed.Should().BeTrue();
	}

	[Fact]
	public void WithDisposableItem_WhenCalled_ShouldDispose()
	{
		var policy = new PoolPolicy<DisposableTestObject>(() => new());
		var item   = new DisposableTestObject();

		policy.OnDiscard(item);

		item.IsDisposed.Should().BeTrue();
	}

	[Fact]
	public void WithOnDiscardDelegate_WhenCalled_ShouldInvokeDelegate()
	{
		var discardCalled = false;
		var policy = new PoolPolicy<object>(
			() => new(),
			onDiscard: _ => discardCalled = true
		);

		var item = new object();

		policy.OnDiscard(item);

		discardCalled.Should().BeTrue();
	}
}
