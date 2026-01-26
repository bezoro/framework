using System;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public class ObjectPoolRentAsyncTests
{
	[Fact]
	public async Task WhenCancelled_ShouldThrowOperationCancelled()
	{
		var pool = new ObjectPool<object>(
			() => new(),
			new() { MaxCapacity = 1, EnableAsyncWait = true });

		pool.Rent();
		var cts = new CancellationTokenSource();
		cts.Cancel();

		var act = async () => await pool.RentAsync(cts.Token);

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task WhenPoolHasItems_ShouldReturnImmediately()
	{
		var    pool     = new ObjectPool<object>(() => new());
		object original = pool.Rent();
		pool.Return(original);

		object item = await pool.RentAsync();

		item.Should().BeSameAs(original);
	}

	[Fact]
	public async Task WithTimeout_WhenItemBecomeAvailable_ShouldReturn()
	{
		var pool = new ObjectPool<object>(
			() => new(),
			new() { MaxCapacity = 1, EnableAsyncWait = true });

		object item1 = pool.Rent();

		var rentTask = pool.RentAsync(TimeSpan.FromSeconds(5));
		_ = Task.Run(async () =>
		{
			await Task.Delay(50);
			pool.Return(item1);
		});

		object? item2 = await rentTask;
		item2.Should().NotBeNull();
	}

	[Fact]
	public async Task WithTimeout_WhenNoItemAvailable_ShouldReturnNull()
	{
		var pool = new ObjectPool<object>(
			() => new(),
			new() { MaxCapacity = 1, EnableAsyncWait = true });

		pool.Rent();

		object? item = await pool.RentAsync(TimeSpan.FromMilliseconds(10));

		item.Should().BeNull();
	}
}
