using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Grid2D<>))]
public class Grid2DConstructorTests
{
	[Fact]
	public void Grid2DConstructor_WhenCalledWithDefaultValue_ShouldInitializesAllCells()
	{
		// Arrange
		const int    WIDTH         = 3;
		const int    HEIGHT        = 4;
		const string DEFAULT_VALUE = "default";

		// Act
		var grid = new Grid2D<string>(WIDTH, HEIGHT, DEFAULT_VALUE);

		// Assert
		grid.Width.Should().Be(WIDTH);
		grid.Height.Should().Be(HEIGHT);

		for (var x = 0; x < WIDTH; x++)
		{
			for (var y = 0; y < HEIGHT; y++)
				grid[x, y].Should().Be(DEFAULT_VALUE);
		}
	}

	[Theory]
	[InlineData(0,  10, 0)]
	[InlineData(10, 0,  0)]
	[InlineData(-1, 10, 0)]
	[InlineData(10, -5, 0)]
	public void Grid2DConstructor_WhenCalledWithDefaultValueAndInvalidDimensions_ShouldThrowsArgumentException(
		int width,
		int height,
		int defaultValue)
	{
		// Act & Assert
		var act = () => new Grid2D<int>(width, height, defaultValue);
		act.Should().Throw<ArgumentException>();
	}

	[Theory]
	[InlineData(0,  10)]
	[InlineData(10, 0)]
	[InlineData(-1, 10)]
	[InlineData(10, -5)]
	public void Grid2DConstructor_WhenDimensionsAreInvalid_ShouldThrowsArgumentException(int width, int height)
	{
		// Act & Assert
		var act = () => new Grid2D<int>(width, height);
		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void Grid2DConstructor_WhenDimensionsAreValid_ShouldInitializesCorrectly()
	{
		// Arrange
		const int WIDTH  = 5;
		const int HEIGHT = 8;

		// Act
		var grid = new Grid2D<int>(WIDTH, HEIGHT);

		// Assert
		grid.Width.Should().Be(WIDTH);
		grid.Height.Should().Be(HEIGHT);
		grid.Length.Should().Be(WIDTH * HEIGHT);
	}

	[Fact]
	public void Grid2DConstructor_WhenUsingPooling_ShouldDoesNotThrow()
	{
		// Act
		using var grid = new Grid2D<int>(10, 10, true);

		// Assert
		grid.Width.Should().Be(10);
		grid.Height.Should().Be(10);
	}
}
