using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Bezoro.Core.Common.Primitives;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Primitives;

[TestSubject(typeof(SwapbackArray<>))]
public static class SwapbackArrayTests
{
	public class Performance
	{
		[Fact]
		public void Add_WhenAddingManyItems_ShouldGrowEfficiently()
		{
			var arr = new SwapbackArray<int>();

			for (var i = 0; i < 10_000; i++)
				arr.Add(i);

			arr.Count.Should().Be(10_000);
			arr.Capacity.Should().BeLessThan(20_000);
		}

		[Fact]
		public void StressTest_AddRange_ShouldBeEfficient()
		{
			var   arr   = new SwapbackArray<int>();
			int[] batch = Enumerable.Range(0, 1_000).ToArray();

			for (var i = 0; i < 100; i++)
				arr.AddRange((IEnumerable<int>)batch);

			// Verify correctness and reasonable capacity growth
			arr.Count.Should().Be(100_000);
			// With 2× doubling strategy, capacity should be ~131,072 (next power of 2)
			arr.Capacity.Should().BeGreaterThanOrEqualTo(100_000);
			arr.Capacity.Should().BeLessThan(200_000); // Not excessively large
		}

		[Fact]
		public void StressTest_BalancedChurn_ShouldStabilizeCapacity()
		{
			var       arr         = new SwapbackArray<int>();
			const int STABLE_SIZE = 500;

			// Pre-fill to stable size
			for (var i = 0; i < STABLE_SIZE; i++)
				arr.Add(i);

			uint stableCapacity = arr.Capacity;

			// Balanced churn: remove from front, add to back (no net growth)
			for (var i = 0; i < 10_000; i++)
			{
				arr.TryRemoveAt(0);
				arr.Add(i + STABLE_SIZE);
			}

			// Count and capacity should remain stable
			arr.Count.Should().Be(STABLE_SIZE);
			arr.Capacity.Should().Be(stableCapacity);
		}

		[Fact]
		public void StressTest_ChurnPattern_ShouldNotLeakMemory()
		{
			var       arr        = new SwapbackArray<int>();
			const int ITERATIONS = 1_000; // Reduced for reasonable test time

			// Simulate entity system churn: add/remove repeatedly
			for (var i = 0; i < ITERATIONS; i++)
			{
				// Add 100 items
				for (var j = 0; j < 100; j++)
					arr.Add(j);

				// Remove 50 items (net +50 per iteration)
				for (var j = 0; j < 50; j++)
					arr.TryRemoveAt(0);
			}

			// After 1000 iterations: 50,000 items net accumulated
			// With 50% shrink threshold and 2× headroom:
			// Expected capacity ≈ 100,000 (2× count for headroom)
			arr.Count.Should().Be(50_000);
			arr.Capacity.Should().BeGreaterThanOrEqualTo(50_000); // Must hold items
			arr.Capacity.Should().BeLessThan(150_000);            // But not excessively large
		}

		[Fact]
		public void StressTest_GrowAndShrink_ShouldReclaimMemory()
		{
			var arr = new SwapbackArray<int>();

			// Grow large
			for (var i = 0; i < 10_000; i++)
				arr.Add(i);

			uint largeCapacity = arr.Capacity;

			// Shrink back down
			while (arr.Count > 100)
				arr.TryRemoveAt(0);

			// Capacity should shrink (not stay at large size)
			arr.Capacity.Should().BeLessThan(largeCapacity / 4);
		}

		[Fact]
		public void StressTest_GrowthPattern_ShouldMaintainReasonableCapacity()
		{
			var       arr        = new SwapbackArray<int>();
			const int ITERATIONS = 1_000;

			// Simulate gradual growth: net +50 items per iteration
			for (var i = 0; i < ITERATIONS; i++)
			{
				for (var j = 0; j < 100; j++)
					arr.Add(j);

				for (var j = 0; j < 50; j++)
					arr.TryRemoveAt(0);
			}

			// After 1000 iterations: 50,000 items
			// Expected capacity: ~65,536 (next power of 2)
			arr.Count.Should().Be(50_000);
			arr.Capacity.Should().BeGreaterThanOrEqualTo(50_000); // Must hold all items
			arr.Capacity.Should().BeLessThan(100_000);            // But not excessively large
		}

		[Fact]
		public void StressTest_ManyAdds_ShouldMaintainReasonableCapacity()
		{
			var       arr        = new SwapbackArray<int>();
			const int ITERATIONS = 100_000;

			for (var i = 0; i < ITERATIONS; i++)
				arr.Add(i);

			arr.Count.Should().Be(ITERATIONS);
			// Verify capacity growth is logarithmic, not linear
			arr.Capacity.Should().BeLessThan((uint)ITERATIONS * 2);
		}

		[Fact]
		public void StressTest_RandomRemoval_ShouldMaintainPerformance()
		{
			var       arr    = new SwapbackArray<int>();
			const int SIZE   = 10_000;
			var       random = new Random(42); // Seed for reproducibility

			// Fill array
			for (var i = 0; i < SIZE; i++)
				arr.Add(i);

			// Remove half randomly - O(1) swap-back should handle this easily
			for (var i = 0; i < SIZE / 2; i++)
			{
				var index = (uint)random.Next((int)arr.Count);
				arr.TryRemoveAt(index);
			}

			// Verify correctness and reasonable capacity after shrinking
			arr.Count.Should().Be(SIZE / 2);
			arr.Capacity.Should().BeGreaterThanOrEqualTo(SIZE / 2);
			arr.Capacity.Should().BeLessThan(SIZE * 2); // Shrink should have triggered
		}

		[Fact]
		public void StressTest_RemovalComplexity_ShouldBeConstant()
		{
			// Verify O(1) removal by comparing small vs large arrays
			// If O(1), time ratio should be ~1:1 regardless of array size
			const int SMALL_SIZE = 1_000;
			const int LARGE_SIZE = 100_000;
			const int REMOVALS   = 500;
			var       random     = new Random(42);

			// Small array: fill and time removals
			var smallArr = new SwapbackArray<int>();
			for (var i = 0; i < SMALL_SIZE; i++) smallArr.Add(i);

			var smallSw = Stopwatch.StartNew();
			for (var i = 0; i < REMOVALS; i++)
				smallArr.TryRemoveAt((uint)random.Next((int)smallArr.Count));

			smallSw.Stop();

			// Large array: fill and time removals
			var largeArr = new SwapbackArray<int>();
			for (var i = 0; i < LARGE_SIZE; i++) largeArr.Add(i);

			var largeSw = Stopwatch.StartNew();
			for (var i = 0; i < REMOVALS; i++)
				largeArr.TryRemoveAt((uint)random.Next((int)largeArr.Count));

			largeSw.Stop();

			// O(1) removal: large array should take similar time (within 5× tolerance for noise)
			// O(n) removal would show 100× slowdown
			double ratio = (double)largeSw.ElapsedTicks / Math.Max(smallSw.ElapsedTicks, 1);
			ratio.Should().BeLessThan(
				5.0,
				"O(1) removal should show similar performance regardless of array size");
		}
	}

	public static class UnitTests
	{
		public class Add
		{
			[Fact]
			public void WhenArrayIsFull_ShouldDoubleCapacity()
			{
				const uint INITIAL_CAPACITY = 5u;
				var        arr              = new SwapbackArray<int>(INITIAL_CAPACITY);

				for (var i = 0; i < INITIAL_CAPACITY + 1; i++)
					arr.Add(i);

				arr.Capacity.Should().Be(INITIAL_CAPACITY * 2);
			}

			[Fact]
			public void WhenSuccessful_ShouldAppendItem()
			{
				int[] startingValues = [1, 2, 3];
				// ReSharper disable once UseObjectOrCollectionInitializer
				var arr = new SwapbackArray<int>(startingValues);

				arr.Add(4);

				arr.ToArray().Should().Equal(1, 2, 3, 4);
				arr.Count.Should().Be(4);
			}

			[Fact]
			public void WhenSuccessful_ShouldIncrementVersion()
			{
				var  arr            = new SwapbackArray<int>();
				uint initialVersion = arr.Version;

				arr.Add(1);

				arr.Version.Should().Be(initialVersion + 1);
			}
		}

		public static class AddRange
		{
			public class EnumerableOverload
			{
				[Fact]
				public void WhenEmpty_ShouldNotIncrementVersion()
				{
					var  arr            = new SwapbackArray<int>();
					uint initialVersion = arr.Version;

					arr.AddRange(Enumerable.Empty<int>());

					arr.Version.Should().Be(initialVersion);
				}

				[Fact]
				public void WhenNull_ShouldThrow()
				{
					var arr = new SwapbackArray<int>();

					var act = () => arr.AddRange((IEnumerable<int>)null!);

					act.Should().Throw<ArgumentNullException>().WithParameterName("collection");
				}

				[Fact]
				public void WhenValid_ShouldAddAllItems()
				{
					var arr = new SwapbackArray<int>();

					arr.AddRange((IEnumerable<int>)[1, 2, 3, 4]);

					arr.ToArray().Should().Equal(1, 2, 3, 4);
				}

				[Fact]
				public void WhenValid_ShouldIncrementVersionOnce()
				{
					var  arr            = new SwapbackArray<int>();
					uint initialVersion = arr.Version;

					arr.AddRange(Enumerable.Range(1, 3));

					arr.Version.Should().Be(initialVersion + 1);
				}
			}

			public class SpanOverload
			{
				[Fact]
				public void WhenEmpty_ShouldNotIncrementVersion()
				{
					var  arr            = new SwapbackArray<int>();
					uint initialVersion = arr.Version;

					arr.AddRange(new Span<int>([]));

					arr.Version.Should().Be(initialVersion);
				}

				[Fact]
				public void WhenValid_ShouldAddAllItems()
				{
					var arr = new SwapbackArray<int> { 1, 2 };

					arr.AddRange(new ReadOnlySpan<int>([3, 4, 5]));

					arr.ToArray().Should().Equal(1, 2, 3, 4, 5);
				}

				[Fact]
				public void WhenValid_ShouldIncrementVersionOnce()
				{
					var  arr            = new SwapbackArray<int>();
					uint initialVersion = arr.Version;

					arr.AddRange(new ReadOnlySpan<int>([1, 2, 3]));
					uint finalVersion = arr.Version;

					finalVersion.Should().NotBe(initialVersion);
					arr.Version.Should().Be(finalVersion);
				}
			}
		}

		public class AddUnchecked
		{
			[Fact]
			public void ShouldIncrementVersion()
			{
				var  arr            = new SwapbackArray<int>(10);
				uint initialVersion = arr.Version;

				arr.AddUnchecked(1);
				uint finalVersion = arr.Version;

				arr.Version.Should().NotBe(initialVersion);
				arr.Version.Should().Be(finalVersion);
			}

			[Fact]
			public void WhenSuccessful_ShouldAppendItem()
			{
				var arr = new SwapbackArray<int>(10);

				arr.AddUnchecked(42);

				arr[0].Should().Be(42);
				arr.Count.Should().Be(1);
			}
		}

		public class AsMutableSpanUnsafe
		{
			[Fact]
			public void WhenCalled_ShouldNotIncrementVersion()
			{
				var  arr            = new SwapbackArray<int> { 1, 2, 3 };
				uint initialVersion = arr.Version;

				var span = arr.AsMutableSpanUnsafe();
				span[0] = 99;

				arr.Version.Should().Be(initialVersion);
			}

			[Fact]
			public void WhenCalled_ShouldReturnWritableSpan()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3 };

				var span = arr.AsMutableSpanUnsafe();
				span[1] = 99;

				arr[1].Should().Be(99);
			}
		}

		public class AsSpan
		{
			[Fact]
			public void WhenCalled_ShouldReturnSpanWithExpectedLength()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				var span = arr.AsSpan();

				span.Length.Should().Be((int)arr.Count);
			}

			[Fact]
			public void WhenCalled_ShouldReturnSpanWithExpectedValues()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				var span = arr.AsSpan();

				span.ToArray().Should().Equal(values);
			}
		}

		public class Clear
		{
			[Fact]
			public void WhenSuccessful_ShouldIncrementVersion()
			{
				var  arr            = new SwapbackArray<int> { 1, 2, 3 };
				uint initialVersion = arr.Version;

				arr.Clear();

				arr.Version.Should().Be(initialVersion + 1);
			}

			[Fact]
			public void WhenSuccessful_ShouldResetCountToZero()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3 };

				arr.Clear();

				arr.Count.Should().Be(0);
				arr.IsEmpty.Should().BeTrue();
			}

			[Fact]
			public void WhenSuccessful_WithoutTrim_ShouldMaintainCapacity()
			{
				var arr = new SwapbackArray<int>(100);
				for (var i = 0; i < 50; i++) arr.Add(i);
				uint capacity = arr.Capacity;

				arr.Clear(false);

				arr.Capacity.Should().Be(capacity);
			}

			[Fact]
			public void WhenSuccessful_WithTrim_ShouldShrinkToMinimumCapacity()
			{
				var arr = new SwapbackArray<int>(100);
				for (var i = 0; i < 50; i++) arr.Add(i);

				arr.Clear();

				arr.Capacity.Should().Be(arr.MinimumArraySize);
			}
		}

		public static class Constructors
		{
			public class CollectionOverload
			{
				[Fact]
				public void WhenEmpty_ShouldUseMinimumCapacity()
				{
					var arr = new SwapbackArray<int>(Array.Empty<int>());

					arr.Capacity.Should().Be(4);
					arr.Count.Should().Be(0);
				}

				[Fact]
				public void WhenNull_ShouldThrow()
				{
					var act = () => new SwapbackArray<int>(null!);

					act.Should().Throw<ArgumentNullException>().WithParameterName("collection");
				}

				[Fact]
				public void WhenValid_ShouldCopyElements()
				{
					int[] values = [1, 2, 3, 4];

					var arr = new SwapbackArray<int>(values);

					arr.ToArray().Should().Equal(values);
					arr.Count.Should().Be(4);
					arr.Capacity.Should().BeGreaterThanOrEqualTo(4);
				}
			}

			public class EnumerableOverload
			{
				[Fact]
				public void WhenEmpty_ShouldCreateEmptyArray()
				{
					var values = GetNonCollectionEnumerable();
					var arr    = new SwapbackArray<int>(values);

					arr.Count.Should().Be(0);
					arr.Capacity.Should().Be(arr.MinimumArraySize);
				}

				[Fact]
				public void WhenLargeEnumerable_ShouldGrowCapacityAsNeeded()
				{
					var values = GetNonCollectionEnumerable(Enumerable.Range(0, 100).ToArray());
					var arr    = new SwapbackArray<int>(values);

					arr.Count.Should().Be(100);
					arr.ToArray().Should().Equal(Enumerable.Range(0, 100));
				}

				[Fact]
				public void WhenNull_ShouldThrow()
				{
					var act = () => new SwapbackArray<int>((IEnumerable<int>)null!);

					act.Should().Throw<ArgumentNullException>().WithParameterName("collection");
				}

				[Fact]
				public void WhenValid_ShouldCopyElements()
				{
					var values = GetNonCollectionEnumerable(1, 2, 3, 4);
					var arr    = new SwapbackArray<int>(values);

					arr.ToArray().Should().Equal(1, 2, 3, 4);
					arr.Count.Should().Be(4);
				}

				private static IEnumerable<int> GetNonCollectionEnumerable(params int[] values)
				{
					foreach (int value in values)
						yield return value;
				}
			}

			public class IntOverload
			{
				[Fact]
				public void WhenLessThanMinimumCapacity_ShouldUseMinimumCapacity()
				{
					var arr = new SwapbackArray<int>(3);

					arr.Capacity.Should().Be(4);
				}

				[Fact]
				public void WhenParameterless_ShouldUseMinimumCapacity()
				{
					var arr = new SwapbackArray<int>();

					arr.Capacity.Should().Be(4);
					arr.Count.Should().Be(0);
				}

				[Fact]
				public void WhenValidCapacity_ShouldUseProvidedCapacity()
				{
					var arr = new SwapbackArray<int>(10);

					arr.Capacity.Should().Be(10);
					arr.Count.Should().Be(0);
				}
			}
		}

		public class Contains
		{
			[Fact]
			public void WhenArrayIsEmpty_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int>();

				arr.Contains(1).Should().BeFalse();
			}

			[Fact]
			public void WhenDefaultItemExists_ShouldReturnTrue()
			{
				// ReSharper disable once PreferConcreteValueOverDefault
				var arr = new SwapbackArray<int> { 1, 2, 3, default };

				// ReSharper disable once PreferConcreteValueOverDefault
				arr.Contains(default).Should().BeTrue();
			}

			[Fact]
			public void WhenDefaultItemNotFound_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				// ReSharper disable once PreferConcreteValueOverDefault
				arr.Contains(default).Should().BeFalse();
			}

			[Fact]
			public void WhenItemExists_ShouldReturnTrue()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr.Contains(2).Should().BeTrue();
			}

			[Fact]
			public void WhenItemNotFound_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr.Contains(5).Should().BeFalse();
			}

			[Fact]
			public void WhenNullItemExists_ShouldReturnTrue()
			{
				var arr = new SwapbackArray<int?> { 1, 2, 3, null };

				arr.Contains(null).Should().BeTrue();
			}

			[Fact]
			public void WhenNullItemNotFound_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int?> { 1, 2, 3, 4 };

				arr.Contains(null).Should().BeFalse();
			}
		}

		public static class CopyTo
		{
			public class GenericOverload
			{
				[Fact]
				public void WhenDestinationIndexExceedsLength_ShouldThrow()
				{
					var arr                     = new SwapbackArray<int> { 1, 2 };
					var destination             = new int[5];
					var invalidDestinationIndex = (uint)destination.Length;

					var act = () => arr.CopyTo(destination, invalidDestinationIndex);

					act.Should().Throw<ArgumentException>();
				}

				[Fact]
				public void WhenDestinationIndexProvided_ShouldCopyToOffset()
				{
					int[]      startingValues    = [1, 2, 3];
					var        arr               = new SwapbackArray<int>(startingValues);
					var        destination       = new int[5];
					const uint DESTINATION_INDEX = 2u;
					int[]      expectedResult    = [0, 0, 1, 2, 3];

					arr.CopyTo(destination, DESTINATION_INDEX);

					destination.Should().Equal(expectedResult);
				}

				[Fact]
				public void WhenInsufficientDestinationCapacity_ShouldThrow()
				{
					var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

					var act = () => arr.CopyTo(new int[2]);

					act.Should().Throw<ArgumentException>();
				}

				[Fact]
				public void WhenNullDestination_ShouldThrow()
				{
					var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

					var act = () => arr.CopyTo(null!);

					act.Should().Throw<ArgumentNullException>();
				}

				[Fact]
				public void WhenValidDestination_ShouldCopyAllItems()
				{
					int[] values      = [1, 2, 3, 4];
					var   arr         = new SwapbackArray<int>(values);
					var   destination = new int[4];

					arr.CopyTo(destination);

					destination.Should().Equal(values);
				}
			}

			public class SpanOverload
			{
				[Fact]
				public void WhenDestinationIsTooSmall_ShouldThrow()
				{
					var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

					var act = () => arr.CopyTo(new Span<int>(new int[2]));

					act.Should().Throw<ArgumentException>();
				}

				[Fact]
				public void WhenDestinationIsValid_ShouldCopyAllItems()
				{
					int[] values      = [1, 2, 3, 4];
					var   arr         = new SwapbackArray<int>(values);
					var   destination = new Span<int>(new int[4]);

					arr.CopyTo(destination);

					arr.ToArray().Should().Equal(values);
				}
			}
		}

		public class EnsureCapacity
		{
			[Fact]
			public void WhenArrayIsEmpty_ShouldUseMinimumCapacity()
			{
				var arr = new SwapbackArray<int>();

				arr.EnsureCapacity(1);

				arr.Capacity.Should().BeGreaterThanOrEqualTo(4);
			}

			[Fact]
			public void WhenAtMaxArrayLength_ShouldNotGrow()
			{
				const uint MAX_ARRAY_LENGTH = 0x7FFFFFC7;
				var        arr              = new SwapbackArray<int>(MAX_ARRAY_LENGTH);

				arr.EnsureCapacity(MAX_ARRAY_LENGTH);

				arr.MaxArrayLength.Should().Be(MAX_ARRAY_LENGTH);
			}

			[Fact]
			public void WhenDoublingWouldExceedMax_ShouldUseMaxCapacity()
			{
				var arr = new SwapbackArray<int>(int.MaxValue / 2);

				arr.EnsureCapacity(int.MaxValue / 2 + 1);

				arr.Capacity.Should().Be(arr.MaxArrayLength);
			}

			[Fact]
			public void WhenRequestedMinimumExceedsMaximum_ShouldThrow()
			{
				var arr = new SwapbackArray<int>();
				var act = () => arr.EnsureCapacity(int.MaxValue);
				act.Should().Throw<OutOfMemoryException>();
			}

			[Fact]
			public void WhenValidRequestedMinimumBelowDoubleCapacity_ShouldDoubleCapacity()
			{
				var initialCapacity = 5u;
				var arr             = new SwapbackArray<int>(initialCapacity);

				arr.EnsureCapacity(7);

				arr.Capacity.Should().Be(initialCapacity * 2);
			}

			[Fact]
			public void WhenValidRequestedMinimumIsAboveDoubleCurrentCapacity_ShouldUseMinimum()
			{
				var initialCapacity = 5u;
				var arr             = new SwapbackArray<int>(initialCapacity);

				arr.EnsureCapacity(20);

				arr.Capacity.Should().Be(20);
			}

			[Fact]
			public void WhenValidRequestedMinimumIsBelowCurrentCapacity_ShouldNotChangeCapacity()
			{
				var arr = new SwapbackArray<int>(8);

				arr.EnsureCapacity(6);

				arr.Capacity.Should().Be(8);
			}
		}

		public class GetEnumerator
		{
			[Fact]
			public void WhenArrayIsEmpty_ShouldNotIterate()
			{
				var arr   = new SwapbackArray<int>();
				var count = 0;

				foreach (int _ in arr)
					count++;

				count.Should().Be(0);
			}

			[Fact]
			public void WhenCollectionModifiedDuringEnumeration_ShouldThrow()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				var act = () =>
				{
					foreach (int item in arr)
					{
						if (item == 2)
							arr.Add(99);
					}
				};

				act.Should().Throw<InvalidOperationException>();
			}
		}

		public class Indexer
		{
			[Fact]
			public void WhenGetOutOfBounds_ShouldThrow()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				var act = () => arr[(uint)values.Length];

				act.Should().Throw<ArgumentOutOfRangeException>();
			}

			[Fact]
			public void WhenGetValidIndex_ShouldReturnItem()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr[0].Should().Be(1);
				arr[1].Should().Be(2);
				arr[2].Should().Be(3);
				arr[3].Should().Be(4);
			}

			[Fact]
			public void WhenSetOutOfBounds_ShouldThrow()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				var act = () => arr[(uint)values.Length] = 10;

				act.Should().Throw<ArgumentOutOfRangeException>();
			}

			[Fact]
			public void WhenSetValidIndex_ShouldIncrementVersion()
			{
				var  arr            = new SwapbackArray<int> { 1, 2, 3 };
				uint initialVersion = arr.Version;

				arr[1] = 10;
				uint finalVersion = arr.Version;

				finalVersion.Should().NotBe(initialVersion);
				arr.Version.Should().Be(finalVersion);
			}

			[Fact]
			public void WhenSetValidIndex_ShouldSetItem()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr[1] = 10;

				arr[1].Should().Be(10);
			}
		}

		public class IndexOf
		{
			[Fact]
			public void WhenArrayIsEmpty_ShouldThrow()
			{
				var arr = new SwapbackArray<int>();

				var act = () => arr.IndexOf(1);

				act.Should().Throw<InvalidOperationException>();
			}

			[Fact]
			public void WhenArrayIsNotEmpty_ShouldReturnIndex()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr.IndexOf(2).Should().Be(1);
			}

			[Fact]
			public void WhenReferenceType_ShouldReturnIndex()
			{
				var obj1 = new object();
				var obj2 = new object();
				var obj3 = new object();
				var arr  = new SwapbackArray<object> { obj1, obj2, obj3 };

				arr.IndexOf(obj2).Should().Be(1);
			}
		}

		public class IsEmpty
		{
			[Fact]
			public void WhenArrayIsEmpty_ShouldReturnTrue()
			{
				var arr = new SwapbackArray<int>();

				arr.IsEmpty.Should().BeTrue();
			}

			[Fact]
			public void WhenArrayIsNotEmpty_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3 };

				arr.IsEmpty.Should().BeFalse();
			}
		}

		public class IsFull
		{
			[Fact]
			public void WhenArrayIsFull_ShouldReturnTrue()
			{
				var arr = new SwapbackArray<int>(10);

				for (var i = 0; i < arr.Capacity; i++) arr.Add(i);

				arr.IsFull.Should().BeTrue();
			}

			[Fact]
			public void WhenArrayIsNotFull_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int>(10);

				arr.IsFull.Should().BeFalse();
			}
		}

		public class RemoveAll
		{
			[Fact]
			public void WhenAllMatch_ShouldRemoveAllItems()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

				uint removed = arr.RemoveAll(i => i > 0);

				removed.Should().Be(5);
				arr.Count.Should().Be(0);
			}

			[Fact]
			public void WhenAllMatch_ShouldReturnCorrectCount()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

				uint removed = arr.RemoveAll(i => i > 0);

				removed.Should().Be(5);
			}

			[Fact]
			public void WhenArrayIsEmpty_ShouldNotIncrementVersion()
			{
				var  arr            = new SwapbackArray<int>();
				uint initialVersion = arr.Version;

				arr.RemoveAll(i => i > 0);

				arr.Version.Should().Be(initialVersion);
			}

			[Fact]
			public void WhenArrayIsEmpty_ShouldReturnZero()
			{
				var arr = new SwapbackArray<int>();

				uint removed = arr.RemoveAll(i => i > 0);

				removed.Should().Be(0);
				arr.Count.Should().Be(0);
			}

			[Fact]
			public void WhenComplexPredicate_ShouldWork()
			{
				var arr = new SwapbackArray<int> { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

				arr.RemoveAll(i => i >= 30 && i <= 70 && i % 10 == 0);

				arr.Count.Should().Be(5);
				arr.Should().Contain(10);
				arr.Should().Contain(20);
				arr.Should().Contain(80);
				arr.Should().Contain(90);
				arr.Should().Contain(100);
				arr.Should().NotContain(30);
				arr.Should().NotContain(40);
				arr.Should().NotContain(50);
				arr.Should().NotContain(60);
				arr.Should().NotContain(70);
			}

			[Fact]
			public void WhenFindsItems_ShouldRemoveItems()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

				arr.RemoveAll(i => i % 2 == 0);

				arr.Count.Should().Be(5);
				arr.Should().Contain(1);
				arr.Should().Contain(3);
				arr.Should().Contain(5);
				arr.Should().Contain(7);
				arr.Should().Contain(9);
				arr.Should().NotContain(2);
				arr.Should().NotContain(4);
				arr.Should().NotContain(6);
				arr.Should().NotContain(8);
				arr.Should().NotContain(10);
				// Verify order is preserved (compaction preserves relative order)
				arr.ToArray().Should().Equal(1, 3, 5, 7, 9);
			}

			[Fact]
			public void WhenItemsRemoved_ShouldIncrementVersion()
			{
				var  arr            = new SwapbackArray<int> { 1, 2, 3, 4, 5 };
				uint initialVersion = arr.Version;

				arr.RemoveAll(i => i % 2 == 0);
				uint finalVersion = arr.Version;

				finalVersion.Should().NotBe(initialVersion);
				arr.Version.Should().Be(finalVersion);
			}

			[Fact]
			public void WhenNoMatches_ShouldNotIncrementVersion()
			{
				var  arr            = new SwapbackArray<int> { 1, 2, 3 };
				uint initialVersion = arr.Version;

				arr.RemoveAll(i => i > 10);

				arr.Version.Should().Be(initialVersion);
			}

			[Fact]
			public void WhenNoMatches_ShouldNotModifyArray()
			{
				var   arr      = new SwapbackArray<int> { 1, 2, 3, 4, 5 };
				int[] original = arr.ToArray();

				arr.RemoveAll(i => i > 10);

				arr.ToArray().Should().Equal(original);
				arr.Count.Should().Be(5);
			}

			[Fact]
			public void WhenNoMatches_ShouldReturnZero()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

				uint removed = arr.RemoveAll(i => i > 10);

				removed.Should().Be(0);
				arr.Count.Should().Be(5);
			}

			[Fact]
			public void WhenNullableType_ShouldHandleNulls()
			{
				var arr = new SwapbackArray<int?> { 1, null, 3, null, 5 };

				uint removed = arr.RemoveAll(i => i == null);

				removed.Should().Be(2);
				arr.Count.Should().Be(3);
				arr.Should().NotContain((int?)null);
				arr.Should().Contain(1);
				arr.Should().Contain(3);
				arr.Should().Contain(5);
			}

			[Fact]
			public void WhenNullPredicate_ShouldThrow()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3 };

				var act = () => arr.RemoveAll(null!);

				act.Should().Throw<ArgumentNullException>().WithParameterName("match");
			}

			[Fact]
			public void WhenPredicateThrows_ShouldPropagateException()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

				var act = () => arr.RemoveAll(i => throw new InvalidOperationException("Test exception"));

				act.Should().Throw<InvalidOperationException>().WithMessage("Test exception");
			}

			[Fact]
			public void WhenReferenceType_ShouldClearRemovedSlots()
			{
				var obj1 = new object();
				var obj2 = new object();
				var obj3 = new object();
				var arr  = new SwapbackArray<object> { obj1, obj2, obj3 };

				arr.RemoveAll(o => o == obj2);

				arr.Count.Should().Be(2);
				arr.Contains(obj1).Should().BeTrue();
				arr.Contains(obj2).Should().BeFalse();
				arr.Contains(obj3).Should().BeTrue();
			}

			[Fact]
			public void WhenRemovingAdjacentItems_ShouldWork()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

				arr.RemoveAll(i => i is 2 or 3);

				arr.Count.Should().Be(3);
				arr.Should().Contain(1);
				arr.Should().Contain(4);
				arr.Should().Contain(5);
				arr.Should().NotContain(2);
				arr.Should().NotContain(3);
				// Verify order is preserved
				arr.ToArray().Should().Equal(1, 4, 5);
			}

			[Fact]
			public void WhenRemovingAllDuplicates_ShouldRemoveAllInstances()
			{
				var arr = new SwapbackArray<int> { 1, 2, 2, 2, 3, 4, 2, 5 };

				uint removed = arr.RemoveAll(i => i == 2);

				removed.Should().Be(4);
				arr.Count.Should().Be(4);
				arr.Should().NotContain(2);
				arr.Should().Contain(1);
				arr.Should().Contain(3);
				arr.Should().Contain(4);
				arr.Should().Contain(5);
			}

			[Fact]
			public void WhenRemovingFirstItem_ShouldWork()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

				arr.RemoveAll(i => i == 1);

				arr.Count.Should().Be(4);
				arr.Should().NotContain(1);
				arr.Should().Contain(2);
				arr.Should().Contain(3);
				arr.Should().Contain(4);
				arr.Should().Contain(5);
				// Verify order is preserved
				arr.ToArray().Should().Equal(2, 3, 4, 5);
			}

			[Fact]
			public void WhenRemovingFromLargeArray_ShouldHandleCorrectly()
			{
				var arr = new SwapbackArray<int>();
				for (var i = 0; i < 100; i++)
					arr.Add(i);

				uint removed = arr.RemoveAll(i => i % 2 == 0);

				removed.Should().Be(50);
				arr.Count.Should().Be(50);
				// Verify all odd numbers are present
				for (var i = 1; i < 100; i += 2)
					arr.Should().Contain(i);
			}

			[Fact]
			public void WhenRemovingLastItem_ShouldWork()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

				arr.RemoveAll(i => i == 5);

				arr.Count.Should().Be(4);
				arr.Should().NotContain(5);
				arr.Should().Contain(1);
				arr.Should().Contain(2);
				arr.Should().Contain(3);
				arr.Should().Contain(4);
			}

			[Fact]
			public void WhenRemovingMiddleItem_ShouldWork()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

				arr.RemoveAll(i => i == 3);

				arr.Count.Should().Be(4);
				arr.Should().NotContain(3);
				arr.Should().Contain(1);
				arr.Should().Contain(2);
				arr.Should().Contain(4);
				arr.Should().Contain(5);
			}

			[Fact]
			public void WhenRemovingMultipleItems_ShouldPreserveRemainingItems()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

				arr.RemoveAll(i => i % 3 == 0);

				arr.Count.Should().Be(7);
				arr.Should().Contain(1);
				arr.Should().Contain(2);
				arr.Should().Contain(4);
				arr.Should().Contain(5);
				arr.Should().Contain(7);
				arr.Should().Contain(8);
				arr.Should().Contain(10);
				arr.Should().NotContain(3);
				arr.Should().NotContain(6);
				arr.Should().NotContain(9);
				// Verify order is preserved
				arr.ToArray().Should().Equal(1, 2, 4, 5, 7, 8, 10);
			}

			[Fact]
			public void WhenRemovingSingleItem_ShouldWork()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3 };

				uint removed = arr.RemoveAll(i => i == 2);

				removed.Should().Be(1);
				arr.Count.Should().Be(2);
				arr.Should().Contain(1);
				arr.Should().Contain(3);
				arr.Should().NotContain(2);
			}

			[Fact]
			public void WhenSingleElement_ShouldWork()
			{
				var arr = new SwapbackArray<int> { 42 };

				uint removed = arr.RemoveAll(i => i == 42);

				removed.Should().Be(1);
				arr.Count.Should().Be(0);
			}

			[Fact]
			public void WhenSingleElement_WhenNoMatch_ShouldNotRemove()
			{
				var arr = new SwapbackArray<int> { 42 };

				uint removed = arr.RemoveAll(i => i == 0);

				removed.Should().Be(0);
				arr.Count.Should().Be(1);
				arr.Should().Contain(42);
			}

			[Fact]
			public void WhenSomeItemsMatch_ShouldReturnCorrectCount()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

				uint removed = arr.RemoveAll(i => i % 2 == 0);

				removed.Should().Be(5);
			}

			[Fact]
			public void WhenSomeItemsMatch_ShouldUpdateCount()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

				arr.RemoveAll(i => i % 2 == 0);

				arr.Count.Should().Be(5);
			}

			[Fact]
			public void WhenUnderutilizedAfterRemoval_ShouldTriggerShrink()
			{
				var arr = new SwapbackArray<int>(16);
				for (var i = 0; i < 16; i++)
					arr.Add(i);

				uint initialCapacity = arr.Capacity;
				arr.Capacity.Should().BeGreaterThanOrEqualTo(16);

				// Remove items to trigger shrink threshold (25%)
				arr.RemoveAll(i => i < 12); // Remove 12 items, leaving 4

				arr.Count.Should().Be(4);
				// Capacity should shrink (based on 25% threshold and 2x headroom)
				arr.Capacity.Should().BeLessThan(initialCapacity);
			}
		}

		public class ToArray
		{
			[Fact]
			public void WhenCalled_ShouldReturnArrayWithSameLengthAsArray()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				int[] arr2 = arr.ToArray();

				arr2.Length.Should().Be((int)arr.Count);
			}

			[Fact]
			public void WhenCalled_ShouldReturnArrayWithSameValuesAsArray()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				int[] arr2 = arr.ToArray();

				arr2.Should().Equal(arr);
			}

			[Fact]
			public void WhenCalled_ShouldReturnCopyOfArray()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				int[] arr2 = arr.ToArray();

				arr2.Should().NotBeSameAs(arr);
			}

			[Fact]
			public void WhenEmpty_ShouldReturnEmptyArray()
			{
				var arr = new SwapbackArray<int>();

				int[] arr2 = arr.ToArray();

				arr2.Should().BeEmpty();
			}
		}

		public class TrimExcess
		{
			[Fact]
			public void WhenAtMinimumCapacity_ShouldNotShrink()
			{
				var arr = new SwapbackArray<int> { 1 };

				arr.TrimExcess();

				arr.Capacity.Should().Be(4);
			}

			[Fact]
			public void WhenUtilizationAtOrAboveTrimThreshold_ShouldNotTrim()
			{
				const uint INITIAL_CAPACITY = 100u;
				var        arr              = new SwapbackArray<int>(INITIAL_CAPACITY);
				var        threshold        = 90u;
				for (var i = 0; i < threshold; i++) arr.Add(i);

				arr.TrimExcess();

				arr.Capacity.Should().Be(INITIAL_CAPACITY);
			}

			[Fact]
			public void WhenUtilizationBelowShrinkThreshold_ShouldTrim()
			{
				const uint INITIAL_CAPACITY = 100u;
				var        arr              = new SwapbackArray<int>(INITIAL_CAPACITY);
				uint       threshold        = 49;
				for (var i = 0; i < threshold; i++) arr.Add(i);

				arr.TrimExcess(Percent.Half);

				arr.Capacity.Should().Be(threshold);
			}
		}

		public class TryGet
		{
			[Fact]
			public void WhenIndexEqualsCount_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3 };

				arr.TryGet(arr.Count, out int _).Should().BeFalse();
			}

			[Fact]
			public void WhenIndexIsOutOfBounds_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int?> { 1, 2 };

				arr.TryGet(2, out int? _).Should().BeFalse();
			}

			[Fact]
			public void WhenValidIndex_ShouldReturnItem()
			{
				var arr = new SwapbackArray<int?> { 1, 2 };

				arr.TryGet(0, out int? value).Should().BeTrue();

				value.Should().Be(1);
			}
		}

		public class TryIndexOf
		{
			[Fact]
			public void WhenItemDoesNotExist_ShouldReturnFalseAndNullIndex()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

				bool found = arr.TryIndexOf(6, out uint? index);

				found.Should().BeFalse();
				index.Should().BeNull();
			}

			[Fact]
			public void WhenItemExists_ShouldReturnTrueAndIndex()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

				bool found = arr.TryIndexOf(3, out uint? index);

				found.Should().BeTrue();
				index.Should().Be(2);
			}

			[Fact]
			public void WhenReferenceType_ShouldReturnTrueAndIndex()
			{
				var obj1 = new object();
				var obj2 = new object();
				var obj3 = new object();
				var arr  = new SwapbackArray<object> { obj1, obj2, obj3 };

				bool found = arr.TryIndexOf(obj2, out uint? index);

				found.Should().BeTrue();
				index.Should().Be(1);
			}
		}

		public class TryPopBack
		{
			[Fact]
			public void WhenEmpty_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int>();

				arr.TryPopBack(out int _).Should().BeFalse();
			}

			[Fact]
			public void WhenNotEmpty_ShouldReturnLastItem()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3 };

				arr.TryPopBack(out int value).Should().BeTrue();
				value.Should().Be(3);
			}
		}

		public class TryRemove
		{
			[Fact]
			public void WhenDuplicateItemExists_ShouldRemoveFirstInstance()
			{
				int[] values = [1, 2, 3, 4, 2, 5];
				var   arr    = new SwapbackArray<int>(values);

				arr.TryRemove(2);

				arr.ToArray().Should().Equal(1, 5, 3, 4, 2);
			}

			[Fact]
			public void WhenItemExists_ShouldDecrementCount()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr.TryRemove(2);

				arr.Count.Should().Be(3);
			}

			[Fact]
			public void WhenItemExists_ShouldRemoveItem()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				arr.TryRemove(2);

				arr.ToArray().Should().Equal(1, 4, 3);
			}

			[Fact]
			public void WhenItemExists_ShouldReturnTrue()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr.TryRemove(2).Should().BeTrue();
			}

			[Fact]
			public void WhenItemNotFound_ShouldNotModifyArray()
			{
				int[] values = [1, 2];
				var   arr    = new SwapbackArray<int>(values);

				arr.TryRemove(3);

				arr.ToArray().Should().Equal(values);
			}

			[Fact]
			public void WhenItemNotFound_ShouldReturnFalse()
			{
				int[] values = [1, 2];
				var   arr    = new SwapbackArray<int>(values);

				arr.TryRemove(3).Should().BeFalse();
			}

			[Fact]
			public void WhenReferenceType_ShouldClearRemovedSlot()
			{
				var obj1 = new object();
				var obj2 = new object();
				var arr  = new SwapbackArray<object> { obj1, obj2 };

				arr.TryRemove(obj1);

				arr.Contains(obj1).Should().BeFalse();
			}
		}

		public class TryRemoveAt
		{
			[Fact]
			public void WhenCalled_ShouldDecrementCount()
			{
				var arr = new SwapbackArray<int> { 10, 20, 30, 40 };

				arr.TryRemoveAt(1);

				arr.Count.Should().Be(3);
			}

			[Fact]
			public void WhenCalled_ShouldIncrementVersion()
			{
				var  arr            = new SwapbackArray<int> { 1, 2, 3 };
				uint initialVersion = arr.Version;

				arr.TryRemoveAt(1);
				uint finalVersion = arr.Version;

				finalVersion.Should().NotBe(initialVersion);
				arr.Version.Should().Be(finalVersion);
			}

			[Fact]
			public void WhenCalled_ShouldSwapLastItemIntoRemovedIndex()
			{
				var arr = new SwapbackArray<int> { 10, 20, 30, 40 };

				arr.TryRemoveAt(1);

				arr.ToArray().Should().Equal(10, 40, 30);
			}

			[Fact]
			public void WhenFails_ShouldNotIncrementVersion()
			{
				var  arr            = new SwapbackArray<int> { 1 };
				uint initialVersion = arr.Version;

				arr.TryRemoveAt(99);

				arr.Version.Should().Be(initialVersion);
			}

			[Fact]
			public void WhenIndexIsOutOfBounds_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int> { 10 };

				arr.TryRemoveAt(1).Should().BeFalse();
			}

			[Fact]
			public void WhenRemovingLastElement_ShouldNotSwap()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr.TryRemoveAt(arr.Count - 1);

				arr.ToArray().Should().Equal(1, 2, 3);
				arr.Count.Should().Be(3);
			}

			[Fact]
			public void WhenUnderutilized_ShouldAutoDownsize()
			{
				var arr = new SwapbackArray<int>(16);
				for (var i = 0; i < 16; i++) arr.Add(i);
				arr.Capacity.Should().Be(16);
				arr.Count.Should().Be(16);

				// Remove until count == 4 -> Should resize to 8
				while (arr.Count > 4)
					arr.TryRemoveAt(0).Should().BeTrue();

				arr.Count.Should().Be(4);
				arr.Capacity.Should().Be(8);

				// Remove until count == 2 -> Should resize to 4 (minimum)
				while (arr.Count > 2)
					arr.TryRemoveAt(0).Should().BeTrue();

				arr.Count.Should().Be(2);
				arr.Capacity.Should().Be(4);
			}

			[Fact]
			public void WhenValidIndex_ShouldReturnTrue()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr.TryRemoveAt(1).Should().BeTrue();
			}
		}
	}
}
