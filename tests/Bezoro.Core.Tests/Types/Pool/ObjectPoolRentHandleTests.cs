using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public class ObjectPoolRentHandleTests
{
	[Fact]
	public void ObjectPoolRentHandle_WhenCalled_ShouldShouldReturnHandle()
	{
		var pool = new ObjectPool<object>(() => new());

		using var handle = pool.RentHandle();

		handle.Value.Should().NotBeNull();
	}

	[Fact]
	public void WhenDisposed_WhenCalled_ShouldReturnToPool()
	{
		var    pool = new ObjectPool<object>(() => new());
		object rentedItem;

		using (var handle = pool.RentHandle())
		{
			rentedItem = handle.Value;
		}

		pool.AvailableCount.Should().Be(1);
		pool.TryRent(out object? item);
		item.Should().BeSameAs(rentedItem);
	}
}
