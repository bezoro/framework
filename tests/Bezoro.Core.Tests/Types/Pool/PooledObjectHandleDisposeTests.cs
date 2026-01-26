using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(PooledObjectHandle<>))]
public class PooledObjectHandleDisposeTests
{
	[Fact]
	public void ShouldReturnObjectToPool()
	{
		var    pool = new ObjectPool<object>(() => new());
		object rentedItem;

		using (var handle = pool.RentHandle())
		{
			rentedItem = handle.Value;
		}

		pool.AvailableCount.Should().Be(1);
		pool.TryRent(out object? returned).Should().BeTrue();
		returned.Should().BeSameAs(rentedItem);
	}

	[Fact]
	public void ShouldSetIsDisposedToTrue()
	{
		var pool   = new ObjectPool<object>(() => new());
		var handle = pool.RentHandle();

		handle.IsDisposed.Should().BeFalse();
		handle.Dispose();
		handle.IsDisposed.Should().BeTrue();
	}

	[Fact]
	public void WhenCalledMultipleTimes_ShouldNotThrow()
	{
		var pool   = new ObjectPool<object>(() => new());
		var handle = pool.RentHandle();

		handle.Dispose();
		var act = () => handle.Dispose();

		act.Should().NotThrow();
	}

	[Fact]
	public void WhenCalledMultipleTimes_ShouldOnlyReturnOnce()
	{
		var pool   = new ObjectPool<object>(() => new());
		var handle = pool.RentHandle();

		handle.Dispose();
		handle.Dispose();
		handle.Dispose();

		pool.AvailableCount.Should().Be(1, "item should only be returned once despite multiple Dispose calls");
	}
}
