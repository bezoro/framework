using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayRemoveAllTests
{
	[Fact]
	public void WhenAllMatch_WhenCalled_ShouldRemoveAllItems()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

		uint removed = arr.RemoveAll(i => i > 0);

		removed.Should().Be(5);
		arr.Count.Should().Be(0);
	}

	[Fact]
	public void WhenAllMatch_WhenCalled_ShouldReturnCorrectCount()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

		uint removed = arr.RemoveAll(i => i > 0);

		removed.Should().Be(5);
	}

	[Fact]
	public void WhenArrayIsEmpty_WhenCalled_ShouldNotIncrementVersion()
	{
		var  arr            = new SwapbackArray<int>();
		uint initialVersion = arr.Version;

		arr.RemoveAll(i => i > 0);

		arr.Version.Should().Be(initialVersion);
	}

	[Fact]
	public void WhenArrayIsEmpty_WhenCalled_ShouldReturnZero()
	{
		var arr = new SwapbackArray<int>();

		uint removed = arr.RemoveAll(i => i > 0);

		removed.Should().Be(0);
		arr.Count.Should().Be(0);
	}

	[Fact]
	public void WhenComplexPredicate_WhenCalled_ShouldWork()
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
	public void WhenFindsItems_WhenCalled_ShouldRemoveItems()
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
	public void WhenItemsRemoved_WhenCalled_ShouldIncrementVersion()
	{
		var  arr            = new SwapbackArray<int> { 1, 2, 3, 4, 5 };
		uint initialVersion = arr.Version;

		arr.RemoveAll(i => i % 2 == 0);
		uint finalVersion = arr.Version;

		finalVersion.Should().NotBe(initialVersion);
		arr.Version.Should().Be(finalVersion);
	}

	[Fact]
	public void WhenNoMatches_WhenCalled_ShouldNotIncrementVersion()
	{
		var  arr            = new SwapbackArray<int> { 1, 2, 3 };
		uint initialVersion = arr.Version;

		arr.RemoveAll(i => i > 10);

		arr.Version.Should().Be(initialVersion);
	}

	[Fact]
	public void WhenNoMatches_WhenCalled_ShouldNotModifyArray()
	{
		var   arr      = new SwapbackArray<int> { 1, 2, 3, 4, 5 };
		int[] original = arr.ToArray();

		arr.RemoveAll(i => i > 10);

		arr.ToArray().Should().Equal(original);
		arr.Count.Should().Be(5);
	}

	[Fact]
	public void WhenNoMatches_WhenCalled_ShouldReturnZero()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

		uint removed = arr.RemoveAll(i => i > 10);

		removed.Should().Be(0);
		arr.Count.Should().Be(5);
	}

	[Fact]
	public void WhenNullableType_WhenCalled_ShouldHandleNulls()
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
	public void WhenNullPredicate_WhenCalled_ShouldThrow()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3 };

		var act = () => arr.RemoveAll(null!);

		act.Should().Throw<ArgumentNullException>().WithParameterName("match");
	}

	[Fact]
	public void WhenPredicateThrows_WhenCalled_ShouldPropagateException()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

		var act = () => arr.RemoveAll(i => throw new InvalidOperationException("Test exception"));

		act.Should().Throw<InvalidOperationException>().WithMessage("Test exception");
	}

	[Fact]
	public void WhenReferenceType_WhenCalled_ShouldClearRemovedSlots()
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
	public void WhenRemovingAdjacentItems_WhenCalled_ShouldWork()
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
	public void WhenRemovingAllDuplicates_WhenCalled_ShouldRemoveAllInstances()
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
	public void WhenRemovingFirstItem_WhenCalled_ShouldWork()
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
	public void WhenRemovingFromLargeArray_WhenCalled_ShouldHandleCorrectly()
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
	public void WhenRemovingLastItem_WhenCalled_ShouldWork()
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
	public void WhenRemovingMiddleItem_WhenCalled_ShouldWork()
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
	public void WhenRemovingMultipleItems_WhenCalled_ShouldPreserveRemainingItems()
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
	public void WhenRemovingSingleItem_WhenCalled_ShouldWork()
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
	public void WhenSingleElement_WhenCalled_ShouldWork()
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
	public void WhenSomeItemsMatch_WhenCalled_ShouldReturnCorrectCount()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

		uint removed = arr.RemoveAll(i => i % 2 == 0);

		removed.Should().Be(5);
	}

	[Fact]
	public void WhenSomeItemsMatch_WhenCalled_ShouldUpdateCount()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

		arr.RemoveAll(i => i % 2 == 0);

		arr.Count.Should().Be(5);
	}

	[Fact]
	public void WhenUnderutilizedAfterRemoval_WhenCalled_ShouldTriggerShrink()
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
