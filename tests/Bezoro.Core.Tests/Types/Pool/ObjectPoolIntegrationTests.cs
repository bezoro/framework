using System.Threading.Tasks;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public class ObjectPoolIntegrationTests
{
	[Fact]
	public async Task AsyncRent_WhenPoolExhausted_ShouldWaitForReturn()
	{
		var pool = new ObjectPool<object>(
			() => new(),
			new() { MaxCapacity = 1, EnableAsyncWait = true }
		);

		object item1    = pool.Rent();
		var    rentTask = pool.RentAsync();

		rentTask.IsCompleted.Should().BeFalse();

		pool.Return(item1);

		object item2 = await rentTask;
		item2.Should().NotBeNull();
	}

	[Fact]
	public async Task ConcurrentRentReturn_WhenWithAsyncWait_ShouldMaintainConsistency()
	{
		var pool = new ObjectPool<object>(
			() => new(),
			new() { MaxCapacity = 10, EnableAsyncWait = true }
		);

		var tasks = new Task[20];

		for (var i = 0; i < tasks.Length; i++)
		{
			tasks[i] = Task.Run(async () =>
				{
					for (var j = 0; j < 10; j++)
					{
						object item = await pool.RentAsync();
						await Task.Delay(1);
						pool.Return(item);
					}
				}
			);
		}

		await Task.WhenAll(tasks);

		pool.TotalCount.Should().BeLessThanOrEqualTo(10);
		pool.AvailableCount.Should().BeLessThanOrEqualTo(10);
	}
}
