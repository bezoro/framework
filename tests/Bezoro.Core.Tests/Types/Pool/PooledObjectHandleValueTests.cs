using System;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(PooledObjectHandle<>))]
public class PooledObjectHandleValueTests
{
	[Fact]
	public void PooledObjectHandleValue_WhenCalled_ShouldShouldReturnPooledObject()
	{
		var       pool   = new ObjectPool<object>(() => new());
		using var handle = pool.RentHandle();

		object value = handle.Value;

		value.Should().NotBeNull();
	}

	[Fact]
	public void PooledObjectHandleValue_WhenCalled_ShouldShouldReturnSameObjectOnMultipleAccesses()
	{
		var       pool   = new ObjectPool<object>(() => new());
		using var handle = pool.RentHandle();

		object value1 = handle.Value;
		object value2 = handle.Value;

		value1.Should().BeSameAs(value2);
	}

	[Fact]
	public void WhenDisposed_WhenCalled_ShouldThrowObjectDisposedException()
	{
		var pool   = new ObjectPool<object>(() => new());
		var handle = pool.RentHandle();

		handle.Dispose();
		var act = () => _ = handle.Value;

		act.Should().Throw<ObjectDisposedException>();
	}
}
