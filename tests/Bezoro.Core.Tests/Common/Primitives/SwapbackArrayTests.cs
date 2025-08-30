using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Core.Common.Primitives;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Primitives;

[TestSubject(typeof(SwapbackArray<int>))]
public static class SwapbackArrayTests
{
	public class Unit
	{
		[Fact]
		public void Add_ShouldIncreaseCount_AndResizeWhenFull()
		{
			var arr = new SwapbackArray<int>();

			for (var i = 0; i < 4; i++)
				arr.Add(i);

			arr.Count.Should().Be(4);
			arr.Capacity.Should().Be(4);

			// Trigger resize
			arr.Add(4);
			arr.Count.Should().Be(5);
			arr.Capacity.Should().Be(8);

			arr.TryGet(4, out int value).Should().BeTrue();
			value.Should().Be(4);
		}

		[Fact]
		public void Clear_ShouldEmptyArray_AndTrimCapacityToMinimum()
		{
			var arr = new SwapbackArray<int>();
			for (var i = 0; i < 10; i++) arr.Add(i); // capacity should grow to >= 16
			arr.Capacity.Should().BeGreaterThanOrEqualTo(16);

			arr.Clear();

			arr.Count.Should().Be(0);
			arr.Capacity.Should().Be(4);
			arr.TryGet(0, out int _).Should().BeFalse();
		}

		[Fact]
		public void Constructor_ShouldThrow_WhenInitialCapacityIsNegative()
		{
			var act = () => new SwapbackArray<int>(-1);
			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void Constructor_ShouldUseMinimumCapacity_WhenInitialIsSmaller()
		{
			var arr = new SwapbackArray<int>(0);
			arr.Capacity.Should().BeGreaterThanOrEqualTo(4);
			arr.Count.Should().Be(0);
		}

		[Fact]
		public void Constructor_ShouldUseProvidedCapacity_WhenInitialIsSufficient()
		{
			var arr = new SwapbackArray<int>(10);
			arr.Capacity.Should().Be(10);
			arr.Count.Should().Be(0);
		}

		[Fact]
		public void Remove_ShouldRemoveFirstOccurrence_IfExists()
		{
			var arr = new SwapbackArray<int>();
			arr.Add(1);
			arr.Add(2);
			arr.Add(3);
			arr.Add(2);

			arr.Remove(2).Should().BeTrue();
			arr.Count.Should().Be(3);

			// Gather current values without assuming order
			var values = new List<int>();
			for (var i = 0; i < arr.Count; i++)
			{
				arr.TryGet(i, out int v).Should().BeTrue();
				values.Add(v);
			}

			values.Should().Contain([1, 3, 2]);
			values.Should().HaveCount(3);
			values.Count(x => x == 2).Should().Be(1);
		}

		[Fact]
		public void Remove_ShouldReturnFalse_IfItemNotFound()
		{
			var arr = new SwapbackArray<int>();
			arr.Add(1);
			arr.Add(2);

			arr.Remove(3).Should().BeFalse();
			arr.Count.Should().Be(2);
		}

		[Fact]
		public void RemoveAt_ShouldAutoDownsize_WhenUnderutilized()
		{
			// Start with capacity 16
			var arr = new SwapbackArray<int>(16);
			for (var i = 0; i < 16; i++) arr.Add(i);
			arr.Capacity.Should().Be(16);
			arr.Count.Should().Be(16);

			// Remove until count == 4 -> Should resize to 8
			while (arr.Count > 4)
				arr.RemoveAt(0).Should().BeTrue();

			arr.Count.Should().Be(4);
			arr.Capacity.Should().Be(8);

			// Remove until count == 2 -> Should resize to 4 (minimum)
			while (arr.Count > 2)
				arr.RemoveAt(0).Should().BeTrue();

			arr.Count.Should().Be(2);
			arr.Capacity.Should().Be(4);
		}

		[Fact]
		public void RemoveAt_ShouldReturnFalse_ForInvalidIndex()
		{
			var arr = new SwapbackArray<int>();
			arr.Add(10);

			arr.RemoveAt(-1).Should().BeFalse();
			arr.RemoveAt(1).Should().BeFalse();
			arr.Count.Should().Be(1);
		}

		[Fact]
		public void RemoveAt_ShouldSwapLastIntoRemovedIndex_AndTrimCount()
		{
			var arr = new SwapbackArray<int>();
			arr.Add(10); // index 0
			arr.Add(20); // index 1
			arr.Add(30); // index 2
			arr.Add(40); // index 3

			// Remove element at index 1 (value 20), last (40) should move to index 1
			arr.RemoveAt(1).Should().BeTrue();
			arr.Count.Should().Be(3);

			arr.TryGet(0, out int v0).Should().BeTrue();
			arr.TryGet(1, out int v1).Should().BeTrue();
			arr.TryGet(2, out int v2).Should().BeTrue();

			v0.Should().Be(10);
			v1.Should().Be(40);
			v2.Should().Be(30);

			// Ensure no extra elements accessible
			arr.TryGet(3, out int _).Should().BeFalse();
		}

		[Fact]
		public void TryGet_ShouldReturnFalse_ForInvalidIndex()
		{
			var arr = new SwapbackArray<int?>();
			arr.Add(1);
			arr.Add(2);

			arr.TryGet(-1, out int? v1).Should().BeFalse();
			v1.Should().BeNull();

			arr.TryGet(2, out int? v2).Should().BeFalse();
			v2.Should().BeNull();
		}
	}
}
