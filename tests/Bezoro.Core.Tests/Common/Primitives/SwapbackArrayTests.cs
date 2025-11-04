using System;
using Bezoro.Core.Common.Primitives;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Primitives;

[TestSubject(typeof(SwapbackArray<>))]
public abstract class SwapbackArrayTests
{
	public abstract class UnitTests
	{
		public class Add
		{
			[Fact]
			public void Add_WhenCalledMultipleTimes_ShouldIncreaseCount()
			{
				var arr = new SwapbackArray<int>();

				for (var i = 0; i < 4; i++)
					arr.Add(i);

				arr.Count.Should().Be(4);
				arr.Capacity.Should().Be(4);
			}

			[Fact]
			public void Add_WhenFull_ShouldDoubleCapacity()
			{
				var arr = new SwapbackArray<int>();

				for (var i = 0; i < 5; i++) arr.Add(i);

				arr.Count.Should().Be(5);
				arr.Capacity.Should().Be(8);
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

				(span.Length == arr.Count).Should().BeTrue();
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
			public void Clear_WhenCalled_ShouldEmptyArray()
			{
				var arr = new SwapbackArray<int>();
				for (var i = 0; i < 10; i++) arr.Add(i); // capacity should grow to >= 16
				arr.Capacity.Should().BeGreaterThanOrEqualTo(16);

				arr.Clear();

				arr.Count.Should().Be(0);
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
			public void Constructor_WhenInitialIsSufficient_ShouldUseProvidedCapacity()
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

			#endregion
		}

		public class Contains
		{
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
			public void TrimExcess_WhenArrayHasExcessCapacity_ShouldShrinkCapacityToCount()
			{
				var arr = new SwapbackArray<int>(32) { 1, 2, 3, 4, 5 };

				arr.TrimExcess();

				arr.Capacity.Should().Be(5);
			}
		}

		public class TryGet
		{
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
			public void TryRemove_WhenItemExists_ShouldRemoveItem()
			{
				int[] values = [1, 2, 3, 4];
				var   arr    = new SwapbackArray<int>(values);

				arr.TryRemove(2);

				arr.ToArray().Should().BeEquivalentTo([1, 3, 4]);
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

				arr.ToArray().Should().BeEquivalentTo(values);
			}

			[Fact]
			public void TryRemove_WhenItemNotFound_ShouldReturnFalse()
			{
				int[] values = [1, 2];
				var   arr    = new SwapbackArray<int>(values);

				arr.TryRemove(3).Should().BeFalse();
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
			public void TryRemoveAt_WhenCalled_ShouldSwapLastItemIntoRemovedIndex()
			{
				var arr = new SwapbackArray<int> { 10, 20, 30, 40 };

				arr.TryRemoveAt(1);

				arr.ToArray().Should().BeEquivalentTo([10, 40, 30]);
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
