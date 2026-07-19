using System;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(PooledObjectHandle<>))]
public class PooledObjectHandleDisposeTests
{
	[Fact]
	public void PooledObjectHandleDispose_WhenCalled_ShouldShouldReturnObjectToPool()
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
	public void PooledObjectHandleDispose_WhenCalled_ShouldShouldSetIsDisposedToTrue()
	{
		var pool   = new ObjectPool<object>(() => new());
		var handle = pool.RentHandle();

		handle.IsDisposed.Should().BeFalse();
		handle.Dispose();
		handle.IsDisposed.Should().BeTrue();
	}

	[Fact]
	public void WhenCalledMultipleTimes_WhenCalled_ShouldNotThrow()
	{
		var pool   = new ObjectPool<object>(() => new());
		var handle = pool.RentHandle();

		handle.Dispose();
		var act = () => handle.Dispose();

		act.Should().NotThrow();
	}

	[Fact]
	public void WhenCalledMultipleTimes_WhenCalled_ShouldOnlyReturnOnce()
	{
		var pool   = new ObjectPool<object>(() => new());
		var handle = pool.RentHandle();

		handle.Dispose();
		handle.Dispose();
		handle.Dispose();

		pool.AvailableCount.Should().Be(1, "item should only be returned once despite multiple Dispose calls");
	}

	[Fact]
	public void Dispose_WhenHandleWasCopied_ShouldReturnSharedValueExactlyOnce()
	{
		var pool   = new ObjectPool<object>(() => new());
		var first  = pool.RentHandle();
		var second = first;

		first.Dispose();
		second.Dispose();

		pool.AvailableCount.Should().Be(1);
		first.IsDisposed.Should().BeTrue();
		second.IsDisposed.Should().BeTrue();
	}

	[Fact]
	public void Dispose_WhenCopied_ShouldInvokePoolReturnExactlyOnce()
	{
		var pool   = new CountingPool<object>();
		var first  = new PooledObjectHandle<object>(new(), pool);
		var second = first;

		first.Dispose();
		second.Dispose();

		pool.ReturnCount.Should().Be(1);
	}

	[Fact]
	public void Dispose_WhenCopiedHandlesAreDisposedConcurrently_ShouldInvokePoolReturnExactlyOnce()
	{
		using var start  = new Barrier(2);
		var       pool   = new CountingPool<object>();
		var       first  = new PooledObjectHandle<object>(new(), pool);
		var       second = first;

		Parallel.Invoke(
			() =>
			{
				start.SignalAndWait(TimeSpan.FromSeconds(5)).Should().BeTrue();
				first.Dispose();
			},
			() =>
			{
				start.SignalAndWait(TimeSpan.FromSeconds(5)).Should().BeTrue();
				second.Dispose();
			}
		);

		pool.ReturnCount.Should().Be(1);
	}
}
