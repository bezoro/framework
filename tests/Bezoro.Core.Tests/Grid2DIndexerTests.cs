using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests;

[TestSubject(typeof(Grid2D<>))]
public class Grid2DIndexerTests
{
	[Theory]
	[InlineData(5, 5, 5,  0)]  // x out of bounds
	[InlineData(5, 5, 0,  5)]  // y out of bounds
	[InlineData(5, 5, -1, 0)]  // x negative
	[InlineData(5, 5, 0,  -1)] // y negative
	public void WhenAccessingOutOfBounds_ThenThrowsIndexOutOfRangeException(
		int gridWidth,
		int gridHeight,
		int accessX,
		int accessY)
	{
		// Arrange
		var grid = new Grid2D<int>(gridWidth, gridHeight);

		// Act & Assert
		var act = () => _ = grid[accessX, accessY];
		act.Should().Throw<IndexOutOfRangeException>();
	}

	[Fact]
	public void WhenSettingAndGettingAtBoundaries_ThenWorksCorrectly()
	{
		// Arrange
		const int WIDTH  = 2;
		const int HEIGHT = 2;
		var       grid   = new Grid2D<int>(WIDTH, HEIGHT);

		// Act & Assert for (0,0)
		const int VALUE00 = 1;
		grid[0, 0] = VALUE00;
		grid[0, 0].Should().Be(VALUE00);

		// Act & Assert for (width-1, height-1)
		const int VALUE_WH = 4;
		grid[WIDTH - 1, HEIGHT - 1] = VALUE_WH;
		grid[WIDTH - 1, HEIGHT - 1].Should().Be(VALUE_WH);
	}

	[Fact]
	public void WhenSettingAndGettingValue_ThenStoresAndRetrievesCorrectly()
	{
		// Arrange
		const int    WIDTH      = 5;
		const int    HEIGHT     = 5;
		var          grid       = new Grid2D<double>(WIDTH, HEIGHT);
		const double TEST_VALUE = 3.14;
		const int    TEST_X     = 2;
		const int    TEST_Y     = 3;

		// Act
		grid[TEST_X, TEST_Y] = TEST_VALUE;
		double retrievedValue = grid[TEST_X, TEST_Y];

		// Assert
		retrievedValue.Should().Be(TEST_VALUE);
	}

	[Theory]
	[InlineData(5, 5, 5,  0,  10)] // x out of bounds
	[InlineData(5, 5, 0,  5,  10)] // y out of bounds
	[InlineData(5, 5, -1, 0,  10)] // x negative
	[InlineData(5, 5, 0,  -1, 10)] // y negative
	public void WhenSettingOutOfBounds_ThenThrowsIndexOutOfRangeException(
		int gridWidth,
		int gridHeight,
		int setX,
		int setY,
		int valueToSet)
	{
		// Arrange
		var grid = new Grid2D<int>(gridWidth, gridHeight);

		// Act & Assert
		var act = () => grid[setX, setY] = valueToSet;
		act.Should().Throw<IndexOutOfRangeException>();
	}

	[Fact]
	public void WhenUsingRefReturn_ThenCanModifyInPlace()
	{
		// Arrange
		var grid = new Grid2D<int>(3, 3);
		grid[1, 1] = 10;

		// Act - modify via ref
		ref int cell = ref grid[1, 1];
		cell = 42;

		// Assert
		grid[1, 1].Should().Be(42);
	}
}
