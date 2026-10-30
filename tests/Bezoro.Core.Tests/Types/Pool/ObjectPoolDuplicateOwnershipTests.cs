using System;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Types;
using Bezoro.Core.Types.Pool;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

public sealed class ObjectPoolDuplicateOwnershipTests
{
	[Fact]
	public async Task ForeignConcurrentReturn_ClaimsOwnershipBeforeReset()
	{
		using var entered = new ManualResetEventSlim();
		using var resume = new ManualResetEventSlim();
		int resets = 0;
		using var pool = new ObjectPool<object>(new PoolPolicy<object>(() => new(), reset: _ =>
		{
			if (Interlocked.Increment(ref resets) == 1)
			{
				entered.Set();
				if (!resume.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
			}
			return true;
		}), new PoolOptions(MaxCapacity: 1));
		var foreign = new object();
		Task<bool> first = Task.Run(() => pool.Return(foreign));
		try
		{
			Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
			Assert.False(pool.Return(foreign));
			Assert.Equal(1, resets);
			Assert.False(pool.TryRent(out _));
		}
		finally { resume.Set(); }
		Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(5)));
		Assert.Same(foreign, pool.Rent());
		Assert.False(pool.TryRent(out _));
	}

	[Fact]
	public void DuplicateReturnAfterDispose_DiscardsOwnedObjectOnce()
	{
		int discards = 0;
		var pool = new ObjectPool<object>(new PoolPolicy<object>(() => new(), onDiscard: _ => discards++));
		var item = pool.Rent();
		pool.Dispose();
		Assert.False(pool.Return(item));
		Assert.False(pool.Return(item));
		Assert.Equal(1, discards);
		Assert.Equal(0, pool.TotalCount);
	}

	[Fact]
	public async Task DisposeDuringReset_DuplicateReturnCannotDiscardObjectStillBeingReset()
	{
		using var entered = new ManualResetEventSlim();
		using var resume = new ManualResetEventSlim();
		int discards = 0;
		var pool = new ObjectPool<object>(new PoolPolicy<object>(() => new(), reset: _ =>
		{
			entered.Set();
			if (!resume.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
			return true;
		}, onDiscard: _ => Interlocked.Increment(ref discards)));
		var item = pool.Rent();
		Task<bool> returning = Task.Run(() => pool.Return(item));
		try
		{
			Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
			pool.Dispose();
			Assert.False(pool.Return(item));
			Assert.Equal(0, discards);
		}
		finally { resume.Set(); }
		Assert.False(await returning.WaitAsync(TimeSpan.FromSeconds(5)));
		Assert.Equal(1, discards);
		Assert.Equal(0, pool.TotalCount);
	}

	[Fact]
	public async Task ConcurrentReturnWhileDiscardCallbackRuns_DoesNotResetOrDiscardAgain()
	{
		using var entered = new ManualResetEventSlim();
		using var resume = new ManualResetEventSlim();
		int resets = 0;
		int discards = 0;
		using var pool = new ObjectPool<object>(new PoolPolicy<object>(() => new(),
			reset: _ => { Interlocked.Increment(ref resets); return false; },
			onDiscard: _ =>
			{
				if (Interlocked.Increment(ref discards) == 1)
				{
					entered.Set();
					if (!resume.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
				}
			}), new PoolOptions(MaxCapacity: 1));
		var item = pool.Rent();
		Task<bool> first = Task.Run(() => pool.Return(item));
		try
		{
			Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
			Assert.False(pool.Return(item));
			Assert.Equal(1, resets);
			Assert.Equal(1, discards);
		}
		finally { resume.Set(); }
		Assert.False(await first.WaitAsync(TimeSpan.FromSeconds(5)));
		Assert.Equal(0, pool.TotalCount);
	}
}
