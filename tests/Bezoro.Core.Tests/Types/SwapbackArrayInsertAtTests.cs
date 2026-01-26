using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayInsertAtTests
{
	[Fact]
	public void WhenIndexIsOutOfBounds_ShouldThrow()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		var act = () => arr.InsertAt(10, 5);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void WhenInsertingAtLastIndex_ShouldAppend()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		arr.InsertAt(4, 5);

		arr.Count.Should().Be(5);
		arr.ToArray().Should().Equal(1, 2, 3, 4, 5);
	}

	[Fact]
	public void WhenInsertingAtOccupiedIndex_ShouldIncrementVersion()
	{
		var  arr            = new SwapbackArray<int> { 1, 2, 3 };
		uint initialVersion = arr.Version;

		arr.InsertAt(1, 4);
		uint finalVersion = arr.Version;

		finalVersion.Should().NotBe(initialVersion);
		arr.Version.Should().Be(finalVersion);
	}

	[Fact]
	public void WhenInsertingAtOccupiedIndex_ShouldPerformSwapback()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		arr.InsertAt(1, 5);

		arr.Count.Should().Be(5);
		arr.ToArray().Should().Equal(1, 5, 3, 4, 2);
	}
}
