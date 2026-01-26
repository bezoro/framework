using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests;

[TestSubject(typeof(ReadOnlyGridSpan2D<>))]
public class ReadOnlyGridSpan2DTests
{
	[Fact]
	public void GetRow_ReturnsCorrectReadOnlyRow()
	{
		// Arrange
		ReadOnlySpan<int> data = stackalloc int[] { 1, 2, 3, 4, 5, 6 };
		var               grid = new ReadOnlyGridSpan2D<int>(data, 3, 2);

		// Act
		var row1 = grid.GetRow(1);

		// Assert
		row1.Length.Should().Be(3);
		row1[0].Should().Be(4);
		row1[1].Should().Be(5);
		row1[2].Should().Be(6);
	}

	[Fact]
	public void IsInBounds_ReturnsCorrectResult()
	{
		// Arrange
		ReadOnlySpan<int> data = stackalloc int[9];
		var               grid = new ReadOnlyGridSpan2D<int>(data, 3, 3);

		// Assert
		grid.IsInBounds(0, 0).Should().BeTrue();
		grid.IsInBounds(2, 2).Should().BeTrue();
		grid.IsInBounds(3, 0).Should().BeFalse();
	}

	[Fact]
	public void WhenCreated_ThenCanReadButNotWrite()
	{
		// Arrange
		ReadOnlySpan<int> data = stackalloc int[] { 1, 2, 3, 4, 5, 6 };
		var               grid = new ReadOnlyGridSpan2D<int>(data, 3, 2);

		// Assert
		grid.Width.Should().Be(3);
		grid.Height.Should().Be(2);
		grid[0, 0].Should().Be(1);
		grid[2, 1].Should().Be(6);
	}
}
