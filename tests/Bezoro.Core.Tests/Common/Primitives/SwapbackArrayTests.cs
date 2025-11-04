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

		public class Constructor
		{
			[Fact]
			public void Constructor_WhenInitialCapacityIsNegative_ShouldThrow()
			{
				var act = () => new SwapbackArray<int>(-1);

				act.Should().Throw<ArgumentOutOfRangeException>();
			}

			[Fact]
			public void Constructor_WhenInitialIsSmallerThanMinimum_ShouldThrow()
			{
				var act = () => new SwapbackArray<int>(3);

				act.Should().Throw<ArgumentOutOfRangeException>();
			}

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
			public void TryGet_WhenNegativeIndex_ShouldThrow()
			{
				var arr = new SwapbackArray<int?> { 1, 2 };

				var act = () => arr.TryGet(-1, out int? _);
				act.Should().Throw<ArgumentOutOfRangeException>();
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
				int[] values = new[] { 1, 2 };
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
			public void TryRemoveAt_WhenIndexIsOutOfBounds_ShouldThrow()
			{
				var arr = new SwapbackArray<int> { 10 };

				var act = () => arr.TryRemoveAt(1);
				act.Should().Throw<ArgumentOutOfRangeException>();
			}

			[Fact]
			public void TryRemoveAt_WhenNegativeIndex_ShouldThrow()
			{
				var arr = new SwapbackArray<int> { 10 };

				var act = () => arr.TryRemoveAt(-1);
				act.Should().Throw<ArgumentOutOfRangeException>();
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
