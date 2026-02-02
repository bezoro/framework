using System;
using System.Collections.Generic;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public class ObjectPoolDisposeTests
{
	[Fact]
	public void ShouldDisposeAllPooledItems()
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

		pool.Dispose();

		items.Should().OnlyContain(x => x.IsDisposed);
	}

	[Fact]
	public void ShouldPreventFurtherRent()
	{
		var pool = new ObjectPool<object>(() => new());
		pool.Dispose();

		var act = () => pool.Rent();

		act.Should().Throw<ObjectDisposedException>();
	}

	[Fact]
	public void WhenCalledMultipleTimes_ShouldBeIdempotent()
	{
		var pool = new ObjectPool<object>(() => new());

		pool.Dispose();
		var act = () => pool.Dispose();

		act.Should().NotThrow();
	}
}
