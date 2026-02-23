using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public class ObjectPoolStatisticsTests
{
	[Fact]
	public void WhenTrackingDisabled_WhenCalled_ShouldNotRecordStatistics()
	{
		var pool = new ObjectPool<object>(
			() => new(),
			new(MaxCapacity: -1, TrackStatistics: false)
		);

		pool.Rent();
		pool.Return(pool.Rent());

		var stats = pool.Statistics;
		stats.TotalRented.Should().Be(0);
		stats.TotalCreated.Should().Be(0);
	}

	[Fact]
	public void WhenTrackingEnabled_WhenCalled_ShouldRecordStatistics()
	{
		var pool = new ObjectPool<object>(
			() => new(),
			new(MaxCapacity: -1, TrackStatistics: true)
		);

		object item1 = pool.Rent();
		object item2 = pool.Rent();
		pool.Return(item1);
		pool.Rent();

		var stats = pool.Statistics;
		stats.TotalCreated.Should().Be(2);
		stats.TotalRented.Should().Be(3);
		stats.TotalReturned.Should().Be(1);
	}
}
