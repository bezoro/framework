using System;
using System.Diagnostics;
using System.Linq;
using Bezoro.Core.Common.Primitives;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Primitives;

[TestSubject(typeof(SwapbackArray<>))]
public abstract class SwapbackArrayTests
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

			var sw = Stopwatch.StartNew();
			for (var i = 0; i < 100; i++)
				arr.AddRange(batch);

			sw.Stop();

			arr.Count.Should().Be(100_000);
			// Should be fast: 100 batches of 1000 in < 50ms
			sw.ElapsedMilliseconds.Should().BeLessThan(50);
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

			// Remove half randomly - should be fast due to O(1) swap-back
			var sw = Stopwatch.StartNew();
			for (var i = 0; i < SIZE / 2; i++)
			{
				var index = (uint)random.Next((int)arr.Count);
				arr.TryRemoveAt(index);
			}

			sw.Stop();

			arr.Count.Should().Be(SIZE / 2);
			// Verify O(1) removal: 5000 removals should take < 10ms
			sw.ElapsedMilliseconds.Should().BeLessThan(10);
		}
	}

	public abstract class UnitTests
	{
		public class Add
		{
			[Fact]
			public void Add_WhenArrayIsFull_ShouldAddValueToEndOfArray()
			{
				int[]     startingValues = [1, 2, 3, 4];
				var       swapbackArray  = new SwapbackArray<int>(startingValues);
				const int VALUE_TO_ADD   = 5;
				int[]     expectedValues = startingValues.Concat([VALUE_TO_ADD]).ToArray();

				swapbackArray.Add(VALUE_TO_ADD);

				swapbackArray.ToArray().Should().Equal(expectedValues);
			}

			[Fact]
			public void Add_WhenArrayIsFull_ShouldDoubleArrayCapacityToAccomodateNewValue()
			{
				const uint STARTING_CAPACITY = 5u;
				var        swapbackArray     = new SwapbackArray<int>(STARTING_CAPACITY);

				for (var i = 0; i < STARTING_CAPACITY + 1; i++)
					swapbackArray.Add(i);

				swapbackArray.Capacity.Should().Be(STARTING_CAPACITY * 2);
			}

			[Fact]
			public void Add_WhenSuccessful_ShouldIncrementVersion()
			{
				var  arr            = new SwapbackArray<int>();
				uint initialVersion = arr.Version;

				arr.Add(1);
				uint finalVersion = arr.Version;

				finalVersion.Should().NotBe(initialVersion);
				arr.Version.Should().Be(finalVersion);
			}

			[Fact]
			public void Add_WhenThereIsAvailableSpace_ShouldAddValueToEndOfArray()
			{
				int[]     startingValues = [1, 2, 3];
				var       swapbackArray  = new SwapbackArray<int>(startingValues);
				const int VALUE_TO_ADD   = 4;
				int[]     expectedValues = startingValues.Concat([VALUE_TO_ADD]).ToArray();

				swapbackArray.Add(VALUE_TO_ADD);

				swapbackArray.ToArray().Should().Equal(expectedValues);
			}

			[Fact]
			public void Add_WhenThereIsSpaceAvailable_ShouldIncreaseArrayCount()
			{
				// ReSharper disable once UseObjectOrCollectionInitializer
				var arr = new SwapbackArray<int>();

				arr.Add(1);

				arr.Count.Should().Be(1);
			}
		}

		public class AddRange
		{
			[Fact]
			public void AddRange_WhenCollectionHasElements_ShouldAddAllItems()
			{
				var   arr        = new SwapbackArray<int>();
				int[] collection = [1, 2, 3, 4];

				arr.AddRange(collection);

				arr.ToArray().Should().Equal(collection);
			}

			[Fact]
			public void AddRange_WhenCollectionIsEmpty_ShouldNotModifyArray()
			{
				var   arr        = new SwapbackArray<int>();
				int[] collection = [];

				arr.AddRange(collection);

				arr.Count.Should().Be(0);
			}

			[Fact]
			public void AddRange_WhenCollectionIsNull_ShouldThrow()
			{
				var arr = new SwapbackArray<int>();

				var act = () => arr.AddRange(null!);

				act.Should().Throw<ArgumentNullException>().WithParameterName("collection");
			}

			[Fact]
			public void AddRange_WhenEnumerableIsEmpty_ShouldNotIncrementVersion()
			{
				var  arr            = new SwapbackArray<int>();
				uint initialVersion = arr.Version;

				arr.AddRange([]);

				uint finalVersion = arr.Version;
				finalVersion.Should().Be(initialVersion);
			}

			[Fact]
			public void AddRange_WhenICollection_ShouldIncrementVersionOnce()
			{
				var  arr            = new SwapbackArray<int>();
				uint initialVersion = arr.Version;

				arr.AddRange(new[] { 1, 2, 3 });
				uint finalVersion = arr.Version;

				finalVersion.Should().Be(initialVersion + 1);
				arr.Version.Should().Be(finalVersion);
			}

			[Fact]
			public void AddRange_WhenNonICollection_ShouldIncrementVersionOnce()
			{
				var  arr            = new SwapbackArray<int>();
				uint initialVersion = arr.Version;

				arr.AddRange(Enumerable.Range(1, 3));
				uint finalVersion = arr.Version;

				finalVersion.Should().Be(initialVersion + 1);
				arr.Version.Should().Be(finalVersion);
			}
		}

		public class AsSpan
		{
			[Fact]
			public void AsSpan_WhenCalled_ShouldReturnSpanWithSameLengthAsArray()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				var span = arr.AsSpan();

				span.Length.Should().Be((int)arr.Count);
			}

			[Fact]
			public void AsSpan_WhenCalled_ShouldReturnSpanWithSameValuesAsArray()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				var span = arr.AsSpan();

				span.ToArray().Should().Equal(arr.ToArray());
			}
		}

		public class Clear
		{
			[Fact]
			public void Clear_WhenAlreadyEmpty_ShouldRemainEmpty()
			{
				var arr = new SwapbackArray<int>();

				arr.Clear();

				arr.Count.Should().Be(0);
				arr.Capacity.Should().Be(arr.MinimumArraySize);
			}

			[Fact]
			public void Clear_WhenCalled_ShouldEmptyArray()
			{
				var arr = new SwapbackArray<int>();
				for (var i = 0; i < 10; i++) arr.Add(i); // capacity should grow to >= 16
				arr.Capacity.Should().BeGreaterThanOrEqualTo(16);

				arr.Clear();

				arr.Count.Should().Be(0);
			}

			[Fact]
			public void Clear_WhenCalled_ShouldIncrementVersion()
			{
				var  arr            = new SwapbackArray<int> { 1, 2, 3 };
				uint initialVersion = arr.Version;

				arr.Clear();
				uint finalVersion = arr.Version;

				finalVersion.Should().NotBe(initialVersion);
				arr.Version.Should().Be(finalVersion);
			}

			[Fact]
			public void Clear_WhenCalled_ShouldShrinkCapacity()
			{
				var arr = new SwapbackArray<int>();
				for (var i = 0; i < 10; i++) arr.Add(i); // capacity should grow to >= 16
				arr.Capacity.Should().BeGreaterThanOrEqualTo(16);

				arr.Clear();

				arr.Capacity.Should().Be(4);
				arr.TryGet(0, out int _).Should().BeFalse();
			}
		}

		public class Constructors
		{
			#region Capacity

			[Fact]
			public void Constructor_WhenInitialCapacityIsSufficient_ShouldUseProvidedCapacity()
			{
				var arr = new SwapbackArray<int>(10);

				arr.Capacity.Should().Be(10);
			}

			[Fact]
			public void Constructor_WhenValidInitialCapacity_ShouldCreateEmptyArray()
			{
				var arr = new SwapbackArray<int>(10);

				arr.Count.Should().Be(0);
			}

			[Fact]
			public void Constructor_WhenCapacityIsNotDefined_ShouldUseMinimumCapacity()
			{
				var arr = new SwapbackArray<int>();

				arr.Capacity.Should().Be(4);
			}

			#endregion

			#region ICollection

			[Fact]
			public void Constructor_WhenCollectionIsNull_ShouldThrow()
			{
				var act = () => new SwapbackArray<int>(null!);

				act.Should().Throw<ArgumentNullException>().WithParameterName("collection");
			}

			[Fact]
			public void Constructor_WhenCollectionIsEmpty_ShouldCreateArrayWithMinimumCapacity()
			{
				int[] values = [];
				var   arr    = new SwapbackArray<int>(values);

				arr.Capacity.Should().Be(4);
			}

			[Fact]
			public void Constructor_WhenCollectionHasElements_ShouldCreateArrayWithSameElements()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				arr.ToArray().Should().Equal(values);
			}

			[Fact]
			public void Constructor_WhenCollectionExceedsMinimumCapacity_ShouldUseCollectionCount()
			{
				int[] values = [1, 2, 3, 4, 5, 6, 7, 8];
				var   arr    = new SwapbackArray<int>(values);

				arr.Capacity.Should().Be(8);
				arr.Count.Should().Be(8);
			}

			#endregion
		}

		public class Contains
		{
			[Fact]
			public void Contains_WhenArrayIsEmpty_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int>();

				arr.Contains(1).Should().BeFalse();
			}

			[Fact]
			public void Contains_WhenDefaultItemExists_ShouldReturnTrue()
			{
				// ReSharper disable once PreferConcreteValueOverDefault
				var arr = new SwapbackArray<int> { 1, 2, 3, default };

				// ReSharper disable once PreferConcreteValueOverDefault
				arr.Contains(default).Should().BeTrue();
			}

			[Fact]
			public void Contains_WhenDefaultItemNotFound_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				// ReSharper disable once PreferConcreteValueOverDefault
				arr.Contains(default).Should().BeFalse();
			}

			[Fact]
			public void Contains_WhenItemExists_ShouldReturnTrue()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr.Contains(2).Should().BeTrue();
			}

			[Fact]
			public void Contains_WhenItemNotFound_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr.Contains(5).Should().BeFalse();
			}

			[Fact]
			public void Contains_WhenNullItemExists_ShouldReturnTrue()
			{
				var arr = new SwapbackArray<int?> { 1, 2, 3, null };

				arr.Contains(null).Should().BeTrue();
			}

			[Fact]
			public void Contains_WhenNullItemNotFound_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int?> { 1, 2, 3, 4 };

				arr.Contains(null).Should().BeFalse();
			}
		}

		public class CopyTo
		{
			[Fact]
			public void CopyTo_WhenDestinationIndexExceedsLength_ShouldThrow()
			{
				var arr                     = new SwapbackArray<int> { 1, 2 };
				var destination             = new int[5];
				var invalidDestinationIndex = (uint)destination.Length;

				var act = () => arr.CopyTo(destination, invalidDestinationIndex);

				act.Should().Throw<ArgumentException>();
			}

			[Fact]
			public void CopyTo_WhenDestinationIndexProvided_ShouldCopyToOffset()
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
			public void CopyTo_WhenInsufficientDestinationCapacity_ShouldThrow()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				var act = () => arr.CopyTo(new int[2]);

				act.Should().Throw<ArgumentException>();
			}

			[Fact]
			public void CopyTo_WhenNullDestination_ShouldThrow()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				var act = () => arr.CopyTo(null!);

				act.Should().Throw<ArgumentNullException>();
			}

			[Fact]
			public void CopyTo_WhenValidDestination_ShouldCopyAllItems()
			{
				int[] values      = [1, 2, 3, 4];
				var   arr         = new SwapbackArray<int>(values);
				var   destination = new int[4];

				arr.CopyTo(destination);

				destination.Should().Equal(values);
			}
		}

		public class EnsureCapacity
		{
			[Fact]
			public void EnsureCapacity_WhenArrayIsEmpty_ShouldUseMinimumCapacity()
			{
				var arr = new SwapbackArray<int>();

				arr.EnsureCapacity(1);

				arr.Capacity.Should().BeGreaterThanOrEqualTo(4);
			}

			[Fact]
			public void EnsureCapacity_WhenAtMaxCapacity_ShouldNotGrow()
			{
				const uint MAX_CAPACITY = 0x7FFFFFC7;
				var        arr          = new SwapbackArray<int>(MAX_CAPACITY);

				arr.EnsureCapacity(MAX_CAPACITY);

				arr.MaxCapacity.Should().Be(MAX_CAPACITY);
			}

			[Fact]
			public void EnsureCapacity_WhenDoublingWouldExceedMax_ShouldUseMaxCapacity()
			{
				var arr = new SwapbackArray<int>(int.MaxValue / 2);

				arr.EnsureCapacity(int.MaxValue / 2 + 1);

				arr.Capacity.Should().Be(arr.MaxCapacity);
			}

			[Fact]
			public void EnsureCapacity_WhenRequestedMinimumExceedsMaximum_ShouldThrow()
			{
				var arr = new SwapbackArray<int>();
				var act = () => arr.EnsureCapacity(int.MaxValue);
				act.Should().Throw<OutOfMemoryException>();
			}

			[Fact]
			public void EnsureCapacity_WhenValidRequestedMinimumBelowDoubleCapacity_ShouldDoubleCapacity()
			{
				var initialCapacity = 5u;
				var arr             = new SwapbackArray<int>(initialCapacity);

				arr.EnsureCapacity(7);

				arr.Capacity.Should().Be(initialCapacity * 2);
			}

			[Fact]
			public void EnsureCapacity_WhenValidRequestedMinimumIsAboveDoubleCurrentCapacity_ShouldUseMinimum()
			{
				var initialCapacity = 5u;
				var arr             = new SwapbackArray<int>(initialCapacity);

				arr.EnsureCapacity(20);

				arr.Capacity.Should().Be(20);
			}

			[Fact]
			public void EnsureCapacity_WhenValidRequestedMinimumIsBelowCurrentCapacity_ShouldNotChangeCapacity()
			{
				var arr = new SwapbackArray<int>(8);

				arr.EnsureCapacity(6);

				arr.Capacity.Should().Be(8);
			}
		}

		public class GetEnumerator
		{
			[Fact]
			public void GetEnumerator_WhenArrayIsEmpty_ShouldNotIterate()
			{
				var arr   = new SwapbackArray<int>();
				var count = 0;

				foreach (int _ in arr)
					count++;

				count.Should().Be(0);
			}

			[Fact]
			public void GetEnumerator_WhenCollectionModifiedDuringEnumeration_ShouldThrow()
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
			public void Indexer_WhenGetOutOfBounds_ShouldThrow()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				var act = () => arr[(uint)values.Length];

				act.Should().Throw<ArgumentOutOfRangeException>();
			}

			[Fact]
			public void Indexer_WhenGetValidIndex_ShouldReturnItem()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr[0].Should().Be(1);
				arr[1].Should().Be(2);
				arr[2].Should().Be(3);
				arr[3].Should().Be(4);
			}

			[Fact]
			public void Indexer_WhenSetOutOfBounds_ShouldThrow()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				var act = () => arr[(uint)values.Length] = 10;

				act.Should().Throw<ArgumentOutOfRangeException>();
			}

			[Fact]
			public void Indexer_WhenSetValidIndex_ShouldIncrementVersion()
			{
				var  arr            = new SwapbackArray<int> { 1, 2, 3 };
				uint initialVersion = arr.Version;

				arr[1] = 10;
				uint finalVersion = arr.Version;

				finalVersion.Should().NotBe(initialVersion);
				arr.Version.Should().Be(finalVersion);
			}

			[Fact]
			public void Indexer_WhenSetValidIndex_ShouldSetItem()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr[1] = 10;

				arr[1].Should().Be(10);
			}
		}

		public class ToArray
		{
			[Fact]
			public void ToArray_WhenCalled_ShouldReturnArrayWithSameLengthAsArray()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				int[] arr2 = arr.ToArray();

				arr2.Length.Should().Be((int)arr.Count);
			}

			[Fact]
			public void ToArray_WhenCalled_ShouldReturnArrayWithSameValuesAsArray()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				int[] arr2 = arr.ToArray();

				arr2.Should().Equal(arr);
			}

			[Fact]
			public void ToArray_WhenCalled_ShouldReturnCopyOfArray()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				int[] arr2 = arr.ToArray();

				arr2.Should().NotBeSameAs(arr);
			}

			[Fact]
			public void ToArray_WhenEmpty_ShouldReturnEmptyArray()
			{
				var arr = new SwapbackArray<int>();

				int[] arr2 = arr.ToArray();

				arr2.Should().BeEmpty();
			}
		}

		public class TrimExcess
		{
			[Fact]
			public void TrimExcess_WhenAtMinimumCapacity_ShouldNotShrink()
			{
				var arr = new SwapbackArray<int> { 1 };

				arr.TrimExcess();

				arr.Capacity.Should().Be(4);
			}

			[Fact]
			public void TrimExcess_WhenUtilizationAtOrAboveTrimThreshold_ShouldNotTrim()
			{
				const uint INITIAL_CAPACITY = 100u;
				var        arr              = new SwapbackArray<int>(INITIAL_CAPACITY);
				uint       threshold        = arr.TrimThresholdPercent;
				for (var i = 0; i < threshold; i++) arr.Add(i);

				arr.TrimExcess();

				arr.Capacity.Should().Be(INITIAL_CAPACITY);
			}

			[Fact]
			public void TrimExcess_WhenUtilizationBelowShrinkThreshold_ShouldTrim()
			{
				const uint INITIAL_CAPACITY = 100u;
				var        arr              = new SwapbackArray<int>(INITIAL_CAPACITY);
				uint       threshold        = arr.TrimThresholdPercent - 1;
				for (var i = 0; i < threshold; i++) arr.Add(i);

				arr.TrimExcess();

				arr.Capacity.Should().Be(threshold);
			}
		}

		public class TryGet
		{
			[Fact]
			public void TryGet_WhenIndexEqualsCount_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3 };

				arr.TryGet(arr.Count, out int _).Should().BeFalse();
			}

			[Fact]
			public void TryGet_WhenIndexIsOutOfBounds_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int?> { 1, 2 };

				arr.TryGet(2, out int? _).Should().BeFalse();
			}

			[Fact]
			public void TryGet_WhenValidIndex_ShouldReturnItem()
			{
				var arr = new SwapbackArray<int?> { 1, 2 };

				arr.TryGet(0, out int? value).Should().BeTrue();

				value.Should().Be(1);
			}
		}

		public class TryRemove
		{
			[Fact]
			public void TryRemove_WhenDuplicateItemExists_ShouldRemoveFirstInstance()
			{
				int[] values = [1, 2, 3, 4, 2, 5];
				var   arr    = new SwapbackArray<int>(values);

				arr.TryRemove(2);

				arr.ToArray().Should().Equal(1, 5, 3, 4, 2);
			}

			[Fact]
			public void TryRemove_WhenItemExists_ShouldDecrementCount()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr.TryRemove(2);

				arr.Count.Should().Be(3);
			}

			[Fact]
			public void TryRemove_WhenItemExists_ShouldRemoveItem()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				arr.TryRemove(2);

				arr.ToArray().Should().Equal(1, 4, 3);
			}

			[Fact]
			public void TryRemove_WhenItemExists_ShouldReturnTrue()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr.TryRemove(2).Should().BeTrue();
			}

			[Fact]
			public void TryRemove_WhenItemNotFound_ShouldNotModifyArray()
			{
				int[] values = [1, 2];
				var   arr    = new SwapbackArray<int>(values);

				arr.TryRemove(3);

				arr.ToArray().Should().Equal(values);
			}

			[Fact]
			public void TryRemove_WhenItemNotFound_ShouldReturnFalse()
			{
				int[] values = [1, 2];
				var   arr    = new SwapbackArray<int>(values);

				arr.TryRemove(3).Should().BeFalse();
			}

			[Fact]
			public void TryRemove_WhenReferenceType_ShouldClearRemovedSlot()
			{
				var obj1 = new object();
				var obj2 = new object();
				var arr  = new SwapbackArray<object> { obj1, obj2 };

				arr.TryRemove(obj1);

				arr.Contains(obj1).Should().BeFalse();
			}

			[Fact]
			public void TryRemoveAt_WhenRemovingLastElement_ShouldNotSwap()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr.TryRemoveAt(arr.Count - 1);

				arr.ToArray().Should().Equal(1, 2, 3);
				arr.Count.Should().Be(3);
			}
		}

		public class TryRemoveAt
		{
			[Fact]
			public void TryRemoveAt_WhenCalled_ShouldDecrementCount()
			{
				var arr = new SwapbackArray<int> { 10, 20, 30, 40 };

				arr.TryRemoveAt(1);

				arr.Count.Should().Be(3);
			}

			[Fact]
			public void TryRemoveAt_WhenCalled_ShouldIncrementVersion()
			{
				var  arr            = new SwapbackArray<int> { 1, 2, 3 };
				uint initialVersion = arr.Version;

				arr.TryRemoveAt(1);
				uint finalVersion = arr.Version;

				finalVersion.Should().NotBe(initialVersion);
				arr.Version.Should().Be(finalVersion);
			}

			[Fact]
			public void TryRemoveAt_WhenCalled_ShouldSwapLastItemIntoRemovedIndex()
			{
				var arr = new SwapbackArray<int> { 10, 20, 30, 40 };

				arr.TryRemoveAt(1);

				arr.ToArray().Should().Equal(10, 40, 30);
			}

			[Fact]
			public void TryRemoveAt_WhenFails_ShouldNotIncrementVersion()
			{
				var  arr            = new SwapbackArray<int> { 1 };
				uint initialVersion = arr.Version;

				arr.TryRemoveAt(99);

				arr.Version.Should().Be(initialVersion);
			}

			[Fact]
			public void TryRemoveAt_WhenIndexIsOutOfBounds_ShouldReturnFalse()
			{
				var arr = new SwapbackArray<int> { 10 };

				arr.TryRemoveAt(1).Should().BeFalse();
			}

			[Fact]
			public void TryRemoveAt_WhenUnderutilized_ShouldAutoDownsize()
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
			public void TryRemoveAt_WhenValidIndex_ShouldReturnTrue()
			{
				var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

				arr.TryRemoveAt(1).Should().BeTrue();
			}
		}
	}
}
