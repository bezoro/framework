using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayEnsureCapacityTests
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
