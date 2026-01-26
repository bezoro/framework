using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayCopyToGenericOverloadTests
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
