using System;
using System.Text;
using System.Threading.Tasks;

using Bezoro.Core.Types.Pool;

using FluentAssertions;

using JetBrains.Annotations;

using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(PooledObjectHandle<>))]
public static class PooledObjectHandleTests
{
	public static class UnitTests
	{
		public class Value
		{
			[Fact]
			public void ShouldReturnPooledObject()
			{
				var pool = new ObjectPool<object>(() => new object());
				using var handle = pool.RentHandle();

				var value = handle.Value;

				value.Should().NotBeNull();
			}

			[Fact]
			public void ShouldReturnSameObjectOnMultipleAccesses()
			{
				var pool = new ObjectPool<object>(() => new object());
				using var handle = pool.RentHandle();

				var value1 = handle.Value;
				var value2 = handle.Value;

				value1.Should().BeSameAs(value2);
			}

			[Fact]
			public void WhenDisposed_ShouldThrowObjectDisposedException()
			{
				var pool = new ObjectPool<object>(() => new object());
				var handle = pool.RentHandle();

				handle.Dispose();
				var act = () => _ = handle.Value;

				act.Should().Throw<ObjectDisposedException>();
			}
		}

		public class ImplicitConversion
		{
			[Fact]
			public void ShouldConvertToUnderlyingType()
			{
				// Use StringBuilder (not object) to properly test implicit conversion
				// Assigning to 'object' boxes the struct instead of triggering the operator
				var pool = new ObjectPool<StringBuilder>(() => new StringBuilder());
				using var handle = pool.RentHandle();
				var expectedValue = handle.Value;

				StringBuilder value = handle;

				value.Should().NotBeNull();
				value.Should().BeSameAs(expectedValue);
			}
		}

		public class Dispose
		{
			[Fact]
			public void ShouldReturnObjectToPool()
			{
				var pool = new ObjectPool<object>(() => new object());
				object rentedItem;

				using (var handle = pool.RentHandle())
				{
					rentedItem = handle.Value;
				}

				pool.AvailableCount.Should().Be(1);
				pool.TryRent(out var returned).Should().BeTrue();
				returned.Should().BeSameAs(rentedItem);
			}

			[Fact]
			public void WhenCalledMultipleTimes_ShouldOnlyReturnOnce()
			{
				var pool = new ObjectPool<object>(() => new object());
				var handle = pool.RentHandle();

				handle.Dispose();
				handle.Dispose();
				handle.Dispose();

				pool.AvailableCount.Should().Be(1, "item should only be returned once despite multiple Dispose calls");
			}

			[Fact]
			public void WhenCalledMultipleTimes_ShouldNotThrow()
			{
				var pool = new ObjectPool<object>(() => new object());
				var handle = pool.RentHandle();

				handle.Dispose();
				var act = () => handle.Dispose();

				act.Should().NotThrow();
			}

			[Fact]
			public void ShouldSetIsDisposedToTrue()
			{
				var pool = new ObjectPool<object>(() => new object());
				var handle = pool.RentHandle();

				handle.IsDisposed.Should().BeFalse();
				handle.Dispose();
				handle.IsDisposed.Should().BeTrue();
			}
		}

		public class Integration
		{
			[Fact]
			public void UsingStatement_ShouldAutoReturn()
			{
				var pool = new ObjectPool<object>(() => new object());

				using (pool.RentHandle())
				{
					pool.AvailableCount.Should().Be(0);
				}

				pool.AvailableCount.Should().Be(1);
			}

			[Fact]
			public void NestedUsing_ShouldWorkCorrectly()
			{
				var pool = new ObjectPool<object>(() => new object());

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
			public async Task AsyncUsing_ShouldAutoReturn()
			{
				var pool = new ObjectPool<object>(() => new object());

				await Task.Run(() =>
				{
					using var handle = pool.RentHandle();
					_ = handle.Value;
				});

				pool.AvailableCount.Should().Be(1);
			}
		}
	}
}
