using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests;

[TestSubject(typeof(Grid2D<>))]
public class Grid2DConstructorTests
{
	[Fact]
	public void WhenCalledWithDefaultValue_ThenInitializesAllCells()
	{
		// Arrange
		const int    width        = 3;
		const int    height       = 4;
		const string defaultValue = "default";

		// Act
		var grid = new Grid2D<string>(width, height, defaultValue);

		// Assert
		grid.Width.Should().Be(width);
		grid.Height.Should().Be(height);

		for (var x = 0; x < width; x++)
		{
			for (var y = 0; y < height; y++)
				grid[x, y].Should().Be(defaultValue);
		}
	}

	[Theory]
	[InlineData(0,  10, 0)]
	[InlineData(10, 0,  0)]
	[InlineData(-1, 10, 0)]
	[InlineData(10, -5, 0)]
	public void WhenCalledWithDefaultValueAndInvalidDimensions_ThenThrowsArgumentException(
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
	public void WhenDimensionsAreInvalid_ThenThrowsArgumentException(int width, int height)
	{
		// Act & Assert
		var act = () => new Grid2D<int>(width, height);
		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void WhenDimensionsAreValid_ThenInitializesCorrectly()
	{
		// Arrange
		const int width  = 5;
		const int height = 8;

		// Act
		var grid = new Grid2D<int>(width, height);

		// Assert
		grid.Width.Should().Be(width);
		grid.Height.Should().Be(height);
		grid.Length.Should().Be(width * height);
	}

	[Fact]
	public void WhenUsingPooling_ThenDoesNotThrow()
	{
		// Act
		using var grid = new Grid2D<int>(10, 10, true);

		// Assert
		grid.Width.Should().Be(10);
		grid.Height.Should().Be(10);
	}
}
