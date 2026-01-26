using System;
using Bezoro.Core.Types;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public class ObjectPoolRentTests
{
	[Fact]
	public void WhenAtMaxCapacity_ShouldThrowPoolExhausted()
	{
		var pool = new ObjectPool<object>(
			() => new(),
			new() { MaxCapacity = 1 });

		pool.Rent();

		var act = () => pool.Rent();

		act.Should().Throw<PoolExhaustedException>()
		   .Which.MaxCapacity.Should().Be(1);
	}

	[Fact]
	public void WhenDisposed_ShouldThrowObjectDisposed()
	{
		var pool = new ObjectPool<object>(() => new());
		pool.Dispose();

		var act = () => pool.Rent();

		act.Should().Throw<ObjectDisposedException>();
	}

	[Fact]
	public void WhenPoolEmpty_ShouldCreateNewItem()
	{
		var createCount = 0;
		var pool = new ObjectPool<object>(() =>
		{
			createCount++;
			return new();
		});

		object item = pool.Rent();

		item.Should().NotBeNull();
		createCount.Should().Be(1);
		pool.TotalCount.Should().Be(1);
		pool.AvailableCount.Should().Be(0);
	}

	[Fact]
	public void WhenPoolHasItems_ShouldReturnPooledItem()
	{
		var createCount = 0;
		var pool = new ObjectPool<object>(() =>
		{
			createCount++;
			return new();
		});

		object original = pool.Rent();
		pool.Return(original);

		object rented = pool.Rent();

		rented.Should().BeSameAs(original);
		createCount.Should().Be(1);
	}

	[Fact]
	public void WithIPooledObject_ShouldCallOnRent()
	{
		var pool = new ObjectPool<TestObject>(() => new());

		var item = pool.Rent();

		item.RentCount.Should().Be(1);
	}

	[Fact]
	public void WithValidationEnabled_WhenItemInvalid_ShouldCreateNew()
	{
		var createCount = 0;
		var pool = new ObjectPool<object>(
			new PoolPolicy<object>(
				() =>
				{
					createCount++;
					return new();
				},
				validate: _ => createCount > 1),
			new() { ValidateOnRent = true });

		object item1 = pool.Rent();
		pool.Return(item1);
		object item2 = pool.Rent();

		createCount.Should().Be(2);
		item2.Should().NotBeSameAs(item1);
	}
}
