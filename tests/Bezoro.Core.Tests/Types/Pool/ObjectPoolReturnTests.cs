using System;
using Bezoro.Core.Types;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public class ObjectPoolReturnTests
{
	[Fact]
	public void WhenDisposed_WhenCalled_ShouldDiscardAndReturnFalse()
	{
		var pool = new ObjectPool<DisposableObject>(() => new());
		var item = pool.Rent();
		pool.Dispose();

		bool result = pool.Return(item);

		result.Should().BeFalse();
		item.IsDisposed.Should().BeTrue();
	}

	[Fact]
	public void Return_WhenPoolWasDisposed_ShouldDiscardWithoutResetting()
	{
		var resetCount = 0;
		var discardCount = 0;
		var policy = new PoolPolicy<object>(
			() => new(),
			reset: _ =>
			{
				resetCount++;
				throw new InvalidOperationException("Reset must not run after disposal.");
			},
			onDiscard: _ => discardCount++
		);
		var pool = new ObjectPool<object>(policy, new() { TrackStatistics = true });
		var item = pool.Rent();
		pool.Dispose();
		var returned = true;

		Action returnItem = () => returned = pool.Return(item);

		returnItem.Should().NotThrow();
		returned.Should().BeFalse();
		resetCount.Should().Be(0);
		discardCount.Should().Be(1);
		pool.AvailableCount.Should().Be(0);
		pool.TotalCount.Should().Be(0);
		pool.Statistics.TotalDiscarded.Should().Be(1);
	}

	[Fact]
	public void WhenPoolAtMaxCapacity_WhenCalled_ShouldDiscard()
	{
		// Pool starts with 1 item and max is 1
		var pool = new ObjectPool<object>(
			() => new(),
			new() { MaxCapacity = 1, InitialCapacity = 1 }
		);

		// Don't rent - pool is already full
		var extra = new object();

		// Trying to return extra object should be discarded since pool is full
		bool result = pool.Return(extra);

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenResetReturnsFalse_WhenCalled_ShouldDiscard()
	{
		var pool = new ObjectPool<TestObject>(() => new());
		var item = pool.Rent();
		item.IsValid = false;

		bool result = pool.Return(item);

		result.Should().BeFalse();
		pool.AvailableCount.Should().Be(0);
	}

	[Fact]
	public void WithIPooledObject_WhenCalled_ShouldCallOnReturn()
	{
		var pool = new ObjectPool<TestObject>(() => new());
		var item = pool.Rent();

		pool.Return(item);

		item.ReturnCount.Should().Be(1);
	}

	[Fact]
	public void WithNullItem_WhenCalled_ShouldThrow()
	{
		var pool = new ObjectPool<object>(() => new());

		var act = () => pool.Return(null!);

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void WithValidItem_WhenCalled_ShouldAddToPool()
	{
		var    pool = new ObjectPool<object>(() => new());
		object item = pool.Rent();

		bool result = pool.Return(item);

		result.Should().BeTrue();
		pool.AvailableCount.Should().Be(1);
	}

	[Fact]
	public void Return_WhenItemWasNotCreatedByPool_ShouldAcceptAndOwnAvailableCapacity()
	{
		var pool = new ObjectPool<object>(() => new(), new() { MaxCapacity = 1 });
		var foreign = new object();

		pool.Return(foreign).Should().BeTrue();

		pool.TotalCount.Should().Be(1);
		pool.AvailableCount.Should().Be(1);
		pool.Rent().Should().BeSameAs(foreign);
	}

	[Fact]
	public void Return_WhenForeignItemReplacesRentedOwnedItem_ShouldDiscardLaterUnownedReturnWithoutReleasingCapacity()
	{
		var policy = new TrackingPoolPolicy();
		var pool = new ObjectPool<object>(policy, new() { MaxCapacity = 1, TrackStatistics = true });
		var poolCreatedItem = pool.Rent();
		var foreign = new object();

		pool.Return(foreign).Should().BeTrue();
		pool.Return(poolCreatedItem).Should().BeFalse();

		pool.AvailableCount.Should().Be(1);
		pool.TotalCount.Should().Be(1);
		pool.Statistics.TotalDiscarded.Should().Be(1);
		policy.DiscardCount.Should().Be(1);
	}
}
