using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayTryRemoveAtTests
{
	[Fact]
	public void TryRemoveAt_WhenRemovingPenultimateElement_ShouldSwapLastElement()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3 };
		arr.TryRemoveAt(1);                 // Remove middle element
		arr.ToArray().Should().Equal(1, 3); // Last swapped in
	}

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
