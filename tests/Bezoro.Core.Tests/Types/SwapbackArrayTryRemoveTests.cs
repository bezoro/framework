using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayTryRemoveTests
{
	[Fact]
	public void WhenDuplicateItemExists_WhenCalled_ShouldRemoveFirstInstance()
	{
		int[] values = [1, 2, 3, 4, 2, 5];
		var   arr    = new SwapbackArray<int>(values);

		arr.TryRemove(2);

		arr.ToArray().Should().Equal(1, 5, 3, 4, 2);
	}

	[Fact]
	public void WhenItemExists_WhenCalled_ShouldDecrementCount()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		arr.TryRemove(2);

		arr.Count.Should().Be(3);
	}

	[Fact]
	public void WhenItemExists_WhenCalled_ShouldRemoveItem()
	{
		int[] values = [1, 2, 3, 4];
		var   arr    = new SwapbackArray<int>(values);

		arr.TryRemove(2);

		arr.ToArray().Should().Equal(1, 4, 3);
	}

	[Fact]
	public void WhenItemExists_WhenCalled_ShouldReturnTrue()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		arr.TryRemove(2).Should().BeTrue();
	}

	[Fact]
	public void WhenItemNotFound_WhenCalled_ShouldNotModifyArray()
	{
		int[] values = [1, 2];
		var   arr    = new SwapbackArray<int>(values);

		arr.TryRemove(3);

		arr.ToArray().Should().Equal(values);
	}

	[Fact]
	public void WhenItemNotFound_WhenCalled_ShouldReturnFalse()
	{
		int[] values = [1, 2];
		var   arr    = new SwapbackArray<int>(values);

		arr.TryRemove(3).Should().BeFalse();
	}

	[Fact]
	public void WhenReferenceType_WhenCalled_ShouldClearRemovedSlot()
	{
		var obj1 = new object();
		var obj2 = new object();
		var arr  = new SwapbackArray<object> { obj1, obj2 };

		arr.TryRemove(obj1);

		arr.Contains(obj1).Should().BeFalse();
	}
}
