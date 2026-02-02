using System;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public class ObjectPoolReturnTests
{
	[Fact]
	public void WhenDisposed_ShouldDiscardAndReturnFalse()
	{
		var pool = new ObjectPool<DisposableObject>(() => new());
		var item = pool.Rent();
		pool.Dispose();

		bool result = pool.Return(item);

		result.Should().BeFalse();
		item.IsDisposed.Should().BeTrue();
	}

	[Fact]
	public void WhenPoolAtMaxCapacity_ShouldDiscard()
	{
		// Pool starts with 1 item and max is 1
		var pool = new ObjectPool<object>(
			() => new(),
			new() { MaxCapacity = 1, InitialCapacity = 1 }
		);

		// Don't rent - pool is already full
		var extra = new object();

		// Trying to return extra object should be discarded since pool is full
		bool result = pool.Return(extra);

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenResetReturnsFalse_ShouldDiscard()
	{
		var pool = new ObjectPool<TestObject>(() => new());
		var item = pool.Rent();
		item.IsValid = false;

		bool result = pool.Return(item);

		result.Should().BeFalse();
		pool.AvailableCount.Should().Be(0);
	}

	[Fact]
	public void WithIPooledObject_ShouldCallOnReturn()
	{
		var pool = new ObjectPool<TestObject>(() => new());
		var item = pool.Rent();

		pool.Return(item);

		item.ReturnCount.Should().Be(1);
	}

	[Fact]
	public void WithNullItem_ShouldThrow()
	{
		var pool = new ObjectPool<object>(() => new());

		var act = () => pool.Return(null!);

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void WithValidItem_ShouldAddToPool()
	{
		var    pool = new ObjectPool<object>(() => new());
		object item = pool.Rent();

		bool result = pool.Return(item);

		result.Should().BeTrue();
		pool.AvailableCount.Should().Be(1);
	}
}
