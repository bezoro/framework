using Bezoro.Core.Types;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public class ObjectPoolTrimExcessTests
{
	[Fact]
	public void ObjectPoolTrimExcess_WhenCalled_ShouldShouldRemoveExcessItems()
	{
		var pool = new ObjectPool<object>(
			() => new(),
			new() { InitialCapacity = 10 }
		);

		int removed = pool.TrimExcess(Percent.Ninety);

		removed.Should().BeGreaterThan(0);
		pool.AvailableCount.Should().BeLessThan(10);
	}

	[Fact]
	public void WhenNoExcess_WhenCalled_ShouldReturnZero()
	{
		var pool = new ObjectPool<object>(() => new());

		int removed = pool.TrimExcess();

		removed.Should().Be(0);
	}
}
