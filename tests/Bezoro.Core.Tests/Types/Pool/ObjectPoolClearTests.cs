using System.Collections.Generic;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public class ObjectPoolClearTests
{
	[Fact]
	public void ObjectPoolClear_WhenCalled_ShouldShouldRemoveAllItems()
	{
		var pool = new ObjectPool<object>(
			() => new(),
			new() { InitialCapacity = 5 }
		);

		pool.Clear();

		pool.AvailableCount.Should().Be(0);
		pool.TotalCount.Should().Be(0);
	}

	[Fact]
	public void WithDisposeTrue_WhenCalled_ShouldDisposeItems()
	{
		var pool = new ObjectPool<DisposableObject>(
			() => new(),
			new() { InitialCapacity = 3 }
		);

		var items = new List<DisposableObject>();
		for (var i = 0; i < 3; i++)
		{
			var item = pool.Rent();
			items.Add(item);
			pool.Return(item);
		}

		pool.Clear();

		items.Should().OnlyContain(x => x.IsDisposed);
	}
}
