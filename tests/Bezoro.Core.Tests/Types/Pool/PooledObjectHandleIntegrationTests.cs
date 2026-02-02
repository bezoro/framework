using System.Threading.Tasks;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(PooledObjectHandle<>))]
public class PooledObjectHandleIntegrationTests
{
	[Fact]
	public async Task AsyncUsing_ShouldAutoReturn()
	{
		var pool = new ObjectPool<object>(() => new());

		await Task.Run(() =>
			{
				using var handle = pool.RentHandle();
				_ = handle.Value;
			}
		);

		pool.AvailableCount.Should().Be(1);
	}

	[Fact]
	public void NestedUsing_ShouldWorkCorrectly()
	{
		var pool = new ObjectPool<object>(() => new());

		using (var handle1 = pool.RentHandle())
		{
			using (var handle2 = pool.RentHandle())
			{
				handle1.Value.Should().NotBeSameAs(handle2.Value);
				pool.TotalCount.Should().Be(2);
			}

			pool.AvailableCount.Should().Be(1);
		}

		pool.AvailableCount.Should().Be(2);
	}

	[Fact]
	public void UsingStatement_ShouldAutoReturn()
	{
		var pool = new ObjectPool<object>(() => new());

		using (pool.RentHandle())
		{
			pool.AvailableCount.Should().Be(0);
		}

		pool.AvailableCount.Should().Be(1);
	}
}
