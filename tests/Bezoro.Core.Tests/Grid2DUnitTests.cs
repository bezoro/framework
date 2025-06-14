using System;
using Xunit;

namespace Bezoro.Core.Tests;

public class Grid2DUnitTests
{
	[Fact]
	public void Constructor_WhenCalledWithDefaultValue_ThenInitializesAllCells()
	{
		// Arrange
		const int    width        = 3;
		const int    height       = 4;
		const string defaultValue = "default";

		// Act
		var grid = new Grid2D<string>(width, height, defaultValue);

		// Assert
		Assert.Equal(width,  grid.Width);
		Assert.Equal(height, grid.Height);

		for (var x = 0 ; x < width ; x++)
		{
			for (var y = 0 ; y < height ; y++)
			{
				Assert.Equal(defaultValue, grid[x, y]);
			}
		}
	}

	[Theory]
	[InlineData(0,  10, 0)]
	[InlineData(10, 0,  0)]
	[InlineData(-1, 10, 0)]
	[InlineData(10, -5, 0)]
	public void Constructor_WhenCalledWithDefaultValueAndInvalidDimensions_ThenThrowsArgumentException(
		int width,
		int height,
		int defaultValue) =>
		// Act & Assert
		Assert.Throws<ArgumentException>(() => new Grid2D<int>(width, height, defaultValue));

	[Theory]
	[InlineData(0,  10)]
	[InlineData(10, 0)]
	[InlineData(-1, 10)]
	[InlineData(10, -5)]
	public void Constructor_WhenDimensionsAreInvalid_ThenThrowsArgumentException(int width, int height) =>
		// Act & Assert
		Assert.Throws<ArgumentException>(() => new Grid2D<int>(width, height));

	[Fact]
	public void Constructor_WhenDimensionsAreValid_ThenInitializesDataArray()
	{
		// Arrange
		const int width  = 5;
		const int height = 8;

		// Act
		var grid = new Grid2D<int>(width, height);

		// Assert
		Assert.Equal(width,  grid.Width);
		Assert.Equal(height, grid.Height);

		grid[width - 1, height - 1] = 123;
		Assert.Equal(123, grid[width - 1, height - 1]);
	}

	[Fact]
	public void Constructor_WhenDimensionsAreValid_ThenPropertiesSetCorrectly()
	{
		// Arrange
		var width  = 10;
		var height = 20;

		// Act
		var grid = new Grid2D(width, height);

		// Assert
		Assert.Equal(width,  grid.Width);
		Assert.Equal(height, grid.Height);
	}

	[Theory]
	[InlineData(5, 5, 5,  0)]  // x out of bounds
	[InlineData(5, 5, 0,  5)]  // y out of bounds
	[InlineData(5, 5, -1, 0)]  // x negative
	[InlineData(5, 5, 0,  -1)] // y negative
	public void Indexer_WhenAccessingOutOfBounds_ThenThrowsIndexOutOfRangeException(
		int gridWidth,
		int gridHeight,
		int accessX,
		int accessY)
	{
		// Arrange
		var grid = new Grid2D<int>(gridWidth, gridHeight);

		// Act & Assert
		Assert.Throws<IndexOutOfRangeException>(() => _ = grid[accessX, accessY]);
	}

	[Fact]
	public void Indexer_WhenSettingAndGettingAtBoundaries_ThenWorksCorrectly()
	{
		// Arrange
		const int width  = 2;
		const int height = 2;
		var       grid   = new Grid2D<int>(width, height);

		// Act & Assert for (0,0)
		const int value00 = 1;
		grid[0, 0] = value00;
		Assert.Equal(value00, grid[0, 0]);

		// Act & Assert for (width-1, height-1)
		var valueWH = 4;
		grid[width - 1, height - 1] = valueWH;
		Assert.Equal(valueWH, grid[width - 1, height - 1]);
	}

	[Fact]
	public void Indexer_WhenSettingAndGettingValue_ThenStoresAndRetrievesCorrectly()
	{
		// Arrange
		const int    width     = 5;
		const int    height    = 5;
		var          grid      = new Grid2D<double>(width, height);
		const double testValue = 3.14;
		const int    testX     = 2;
		const int    testY     = 3;

		// Act
		grid[testX, testY] = testValue;
		var retrievedValue = grid[testX, testY];

		// Assert
		Assert.Equal(testValue, retrievedValue);
	}

	[Theory]
	[InlineData(5, 5, 5,  0,  10)] // x out of bounds
	[InlineData(5, 5, 0,  5,  10)] // y out of bounds
	[InlineData(5, 5, -1, 0,  10)] // x negative
	[InlineData(5, 5, 0,  -1, 10)] // y negative
	public void Indexer_WhenSettingOutOfBounds_ThenThrowsIndexOutOfRangeException(
		int gridWidth,
		int gridHeight,
		int setX,
		int setY,
		int valueToSet)
	{
		// Arrange
		var grid = new Grid2D<int>(gridWidth, gridHeight);

		// Act & Assert
		Assert.Throws<IndexOutOfRangeException>(() => grid[setX, setY] = valueToSet);
	}
}
