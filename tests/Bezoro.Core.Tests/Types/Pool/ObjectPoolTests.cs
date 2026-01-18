using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Bezoro.Core.Abstractions;
using Bezoro.Core.Types;
using Bezoro.Core.Types.Pool;

using FluentAssertions;

using JetBrains.Annotations;

using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public static class ObjectPoolTests
{
	private sealed class TestObject : IPooledObject
	{
		public int RentCount { get; private set; }
		public int ReturnCount { get; private set; }
		public bool IsValid { get; set; } = true;

		public void OnRent() => RentCount++;

		public bool OnReturn()
		{
			ReturnCount++;
			return IsValid;
		}
	}

	private sealed class DisposableObject : IDisposable
	{
		public bool IsDisposed { get; private set; }
		public void Dispose() => IsDisposed = true;
	}

	public class IntegrationTests
	{
		[Fact]
		public async Task ConcurrentRentReturn_WithAsyncWait_ShouldMaintainConsistency()
		{
			var pool = new ObjectPool<object>(
				() => new object(),
				new PoolOptions { MaxCapacity = 10, EnableAsyncWait = true });
			var tasks = new Task[20];

			for (var i = 0; i < tasks.Length; i++)
			{
				tasks[i] = Task.Run(async () =>
				{
					for (var j = 0; j < 10; j++)
					{
						var item = await pool.RentAsync();
						await Task.Delay(1);
						pool.Return(item);
					}
				});
			}

			await Task.WhenAll(tasks);

			pool.TotalCount.Should().BeLessThanOrEqualTo(10);
			pool.AvailableCount.Should().BeLessThanOrEqualTo(10);
		}

		[Fact]
		public async Task AsyncRent_WhenPoolExhausted_ShouldWaitForReturn()
		{
			var pool = new ObjectPool<object>(
				() => new object(),
				new PoolOptions { MaxCapacity = 1, EnableAsyncWait = true });

			var item1 = pool.Rent();
			var rentTask = pool.RentAsync();

			rentTask.IsCompleted.Should().BeFalse();

			pool.Return(item1);

			var item2 = await rentTask;
			item2.Should().NotBeNull();
		}
	}

	public static class UnitTests
	{
		public class Constructors
		{
			[Fact]
			public void WithFactory_ShouldCreatePool()
			{
				var pool = new ObjectPool<object>(() => new object());

				pool.AvailableCount.Should().Be(0);
				pool.TotalCount.Should().Be(0);
				pool.MaxCapacity.Should().Be(-1);
			}

			[Fact]
			public void WithFactoryAndOptions_ShouldRespectOptions()
			{
				var pool = new ObjectPool<object>(
					() => new object(),
					new PoolOptions { MaxCapacity = 5, InitialCapacity = 3 });

				pool.AvailableCount.Should().Be(3);
				pool.TotalCount.Should().Be(3);
				pool.MaxCapacity.Should().Be(5);
			}

			[Fact]
			public void WithNullFactory_ShouldThrow()
			{
				var act = () => new ObjectPool<object>((Func<object>)null!);

				act.Should().Throw<ArgumentNullException>();
			}

			[Fact]
			public void WithNullPolicy_ShouldThrow()
			{
				var act = () => new ObjectPool<object>((IPoolPolicy<object>)null!);

				act.Should().Throw<ArgumentNullException>();
			}

			[Fact]
			public void WithInitialCapacity_ShouldPrewarm()
			{
				var createCount = 0;
				var pool = new ObjectPool<object>(
					() =>
					{
						createCount++;
						return new object();
					},
					new PoolOptions { InitialCapacity = 5 });

				createCount.Should().Be(5);
				pool.AvailableCount.Should().Be(5);
			}
		}

		public class Rent
		{
			[Fact]
			public void WhenPoolEmpty_ShouldCreateNewItem()
			{
				var createCount = 0;
				var pool = new ObjectPool<object>(() =>
				{
					createCount++;
					return new object();
				});

				var item = pool.Rent();

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
					return new object();
				});
				var original = pool.Rent();
				pool.Return(original);

				var rented = pool.Rent();

				rented.Should().BeSameAs(original);
				createCount.Should().Be(1);
			}

			[Fact]
			public void WhenAtMaxCapacity_ShouldThrowPoolExhausted()
			{
				var pool = new ObjectPool<object>(
					() => new object(),
					new PoolOptions { MaxCapacity = 1 });
				pool.Rent();

				var act = () => pool.Rent();

				act.Should().Throw<PoolExhaustedException>()
					.Which.MaxCapacity.Should().Be(1);
			}

			[Fact]
			public void WhenDisposed_ShouldThrowObjectDisposed()
			{
				var pool = new ObjectPool<object>(() => new object());
				pool.Dispose();

				var act = () => pool.Rent();

				act.Should().Throw<ObjectDisposedException>();
			}

			[Fact]
			public void WithIPooledObject_ShouldCallOnRent()
			{
				var pool = new ObjectPool<TestObject>(() => new TestObject());

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
							return new object();
						},
						validate: _ => createCount > 1),
					new PoolOptions { ValidateOnRent = true });

				var item1 = pool.Rent();
				pool.Return(item1);
				var item2 = pool.Rent();

				createCount.Should().Be(2);
				item2.Should().NotBeSameAs(item1);
			}
		}

		public class TryRent
		{
			[Fact]
			public void WhenPoolHasItems_ShouldReturnTrue()
			{
				var pool = new ObjectPool<object>(() => new object());
				var original = pool.Rent();
				pool.Return(original);

				var result = pool.TryRent(out var item);

				result.Should().BeTrue();
				item.Should().BeSameAs(original);
			}

			[Fact]
			public void WhenPoolEmpty_ShouldReturnFalse()
			{
				var pool = new ObjectPool<object>(() => new object());

				var result = pool.TryRent(out var item);

				result.Should().BeFalse();
				item.Should().BeNull();
			}

			[Fact]
			public void WhenDisposed_ShouldReturnFalse()
			{
				var pool = new ObjectPool<object>(() => new object());
				pool.Dispose();

				var result = pool.TryRent(out var item);

				result.Should().BeFalse();
				item.Should().BeNull();
			}
		}

		public class Return
		{
			[Fact]
			public void WithValidItem_ShouldAddToPool()
			{
				var pool = new ObjectPool<object>(() => new object());
				var item = pool.Rent();

				var result = pool.Return(item);

				result.Should().BeTrue();
				pool.AvailableCount.Should().Be(1);
			}

			[Fact]
			public void WithNullItem_ShouldThrow()
			{
				var pool = new ObjectPool<object>(() => new object());

				var act = () => pool.Return(null!);

				act.Should().Throw<ArgumentNullException>();
			}

			[Fact]
			public void WhenDisposed_ShouldDiscardAndReturnFalse()
			{
				var pool = new ObjectPool<DisposableObject>(() => new DisposableObject());
				var item = pool.Rent();
				pool.Dispose();

				var result = pool.Return(item);

				result.Should().BeFalse();
				item.IsDisposed.Should().BeTrue();
			}

			[Fact]
			public void WithIPooledObject_ShouldCallOnReturn()
			{
				var pool = new ObjectPool<TestObject>(() => new TestObject());
				var item = pool.Rent();

				pool.Return(item);

				item.ReturnCount.Should().Be(1);
			}

			[Fact]
			public void WhenResetReturnsFalse_ShouldDiscard()
			{
				var pool = new ObjectPool<TestObject>(() => new TestObject());
				var item = pool.Rent();
				item.IsValid = false;

				var result = pool.Return(item);

				result.Should().BeFalse();
				pool.AvailableCount.Should().Be(0);
			}

			[Fact]
			public void WhenPoolAtMaxCapacity_ShouldDiscard()
			{
				// Pool starts with 1 item and max is 1
				var pool = new ObjectPool<object>(
					() => new object(),
					new PoolOptions { MaxCapacity = 1, InitialCapacity = 1 });
				// Don't rent - pool is already full
				var extra = new object();

				// Trying to return extra object should be discarded since pool is full
				var result = pool.Return(extra);

				result.Should().BeFalse();
			}
		}

		public class RentHandle
		{
			[Fact]
			public void ShouldReturnHandle()
			{
				var pool = new ObjectPool<object>(() => new object());

				using var handle = pool.RentHandle();

				handle.Value.Should().NotBeNull();
			}

			[Fact]
			public void WhenDisposed_ShouldReturnToPool()
			{
				var pool = new ObjectPool<object>(() => new object());
				object rentedItem;

				using (var handle = pool.RentHandle())
				{
					rentedItem = handle.Value;
				}

				pool.AvailableCount.Should().Be(1);
				pool.TryRent(out var item);
				item.Should().BeSameAs(rentedItem);
			}
		}

		public class RentAsync
		{
			[Fact]
			public async Task WhenPoolHasItems_ShouldReturnImmediately()
			{
				var pool = new ObjectPool<object>(() => new object());
				var original = pool.Rent();
				pool.Return(original);

				var item = await pool.RentAsync();

				item.Should().BeSameAs(original);
			}

			[Fact]
			public async Task WhenCancelled_ShouldThrowOperationCancelled()
			{
				var pool = new ObjectPool<object>(
					() => new object(),
					new PoolOptions { MaxCapacity = 1, EnableAsyncWait = true });
				pool.Rent();
				var cts = new CancellationTokenSource();
				cts.Cancel();

				var act = async () => await pool.RentAsync(cts.Token);

				await act.Should().ThrowAsync<OperationCanceledException>();
			}

			[Fact]
			public async Task WithTimeout_WhenNoItemAvailable_ShouldReturnNull()
			{
				var pool = new ObjectPool<object>(
					() => new object(),
					new PoolOptions { MaxCapacity = 1, EnableAsyncWait = true });
				pool.Rent();

				var item = await pool.RentAsync(TimeSpan.FromMilliseconds(10));

				item.Should().BeNull();
			}

			[Fact]
			public async Task WithTimeout_WhenItemBecomeAvailable_ShouldReturn()
			{
				var pool = new ObjectPool<object>(
					() => new object(),
					new PoolOptions { MaxCapacity = 1, EnableAsyncWait = true });
				var item1 = pool.Rent();

				var rentTask = pool.RentAsync(TimeSpan.FromSeconds(5));
				_ = Task.Run(async () =>
				{
					await Task.Delay(50);
					pool.Return(item1);
				});

				var item2 = await rentTask;
				item2.Should().NotBeNull();
			}
		}

		public class TrimExcess
		{
			[Fact]
			public void ShouldRemoveExcessItems()
			{
				var pool = new ObjectPool<object>(
					() => new object(),
					new PoolOptions { InitialCapacity = 10 });

				var removed = pool.TrimExcess(Percent.Ninety);

				removed.Should().BeGreaterThan(0);
				pool.AvailableCount.Should().BeLessThan(10);
			}

			[Fact]
			public void WhenNoExcess_ShouldReturnZero()
			{
				var pool = new ObjectPool<object>(() => new object());

				var removed = pool.TrimExcess();

				removed.Should().Be(0);
			}
		}

		public class Clear
		{
			[Fact]
			public void ShouldRemoveAllItems()
			{
				var pool = new ObjectPool<object>(
					() => new object(),
					new PoolOptions { InitialCapacity = 5 });

				pool.Clear();

				pool.AvailableCount.Should().Be(0);
				pool.TotalCount.Should().Be(0);
			}

			[Fact]
			public void WithDisposeTrue_ShouldDisposeItems()
			{
				var pool = new ObjectPool<DisposableObject>(
					() => new DisposableObject(),
					new PoolOptions { InitialCapacity = 3 });
				var items = new List<DisposableObject>();
				for (var i = 0; i < 3; i++)
				{
					var item = pool.Rent();
					items.Add(item);
					pool.Return(item);
				}

				pool.Clear(disposeItems: true);

				items.Should().OnlyContain(x => x.IsDisposed);
			}
		}

		public class Dispose
		{
			[Fact]
			public void ShouldPreventFurtherRent()
			{
				var pool = new ObjectPool<object>(() => new object());
				pool.Dispose();

				var act = () => pool.Rent();

				act.Should().Throw<ObjectDisposedException>();
			}

			[Fact]
			public void ShouldDisposeAllPooledItems()
			{
				var pool = new ObjectPool<DisposableObject>(
					() => new DisposableObject(),
					new PoolOptions { InitialCapacity = 3 });
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
			public void WhenCalledMultipleTimes_ShouldBeIdempotent()
			{
				var pool = new ObjectPool<object>(() => new object());

				pool.Dispose();
				var act = () => pool.Dispose();

				act.Should().NotThrow();
			}
		}

		public class Statistics
		{
			[Fact]
			public void WhenTrackingEnabled_ShouldRecordStatistics()
			{
				var pool = new ObjectPool<object>(
					() => new object(),
					new PoolOptions(MaxCapacity: -1, TrackStatistics: true));

				var item1 = pool.Rent();
				var item2 = pool.Rent();
				pool.Return(item1);
				pool.Rent();

				var stats = pool.Statistics;
				stats.TotalCreated.Should().Be(2);
				stats.TotalRented.Should().Be(3);
				stats.TotalReturned.Should().Be(1);
			}

			[Fact]
			public void WhenTrackingDisabled_ShouldNotRecordStatistics()
			{
				var pool = new ObjectPool<object>(
					() => new object(),
					new PoolOptions(MaxCapacity: -1, TrackStatistics: false));

				pool.Rent();
				pool.Return(pool.Rent());

				var stats = pool.Statistics;
				stats.TotalRented.Should().Be(0);
				stats.TotalCreated.Should().Be(0);
			}
		}
	}
}
