using System;
using NUnit.Framework;

namespace Bezoro.Core.UnitTests;

[TestFixture]
public class Grid2DUnitTests
{
#region Test Methods

	[Test]
	public void Constructor_WhenCalledWithDefaultValue_ThenInitializesAllCells()
	{
		// Arrange
		const int    width        = 3;
		const int    height       = 4;
		const string defaultValue = "default";

		// Act
		var grid = new Grid2D<string>(width, height, defaultValue);

		Assert.Multiple(
			() =>
			{
				// Assert
				Assert.That(grid.Width,  Is.EqualTo(width));
				Assert.That(grid.Height, Is.EqualTo(height));
			});

		for (var x = 0 ; x < width ; x++)
		{
			for (var y = 0 ; y < height ; y++)
			{
				Assert.That(grid[x, y], Is.EqualTo(defaultValue));
			}
		}
	}

	[Test]
	public void Constructor_WhenDimensionsAreValid_ThenInitializesDataArray()
	{
		// Arrange
		const int width  = 5;
		const int height = 8;

		// Act
		var grid = new Grid2D<int>(width, height);

		Assert.Multiple(
			() =>
			{
				// Assert
				Assert.That(grid.Width,  Is.EqualTo(width));
				Assert.That(grid.Height, Is.EqualTo(height));
			});

		grid[width - 1, height - 1] = 123;
		Assert.That(grid[width - 1, height - 1], Is.EqualTo(123));
	}

	[Test]
	public void Constructor_WhenDimensionsAreValid_ThenPropertiesSetCorrectly()
	{
		// Arrange
		var width  = 10;
		var height = 20;

		// Act
		var grid = new Grid2D(width, height);

		Assert.Multiple(
			() =>
			{
				// Assert
				Assert.That(grid.Width,  Is.EqualTo(width));
				Assert.That(grid.Height, Is.EqualTo(height));
			});
	}

	[Test]
	public void Indexer_WhenSettingAndGettingAtBoundaries_ThenWorksCorrectly()
	{
		// Arrange
		const int width  = 2;
		const int height = 2;
		var       grid   = new Grid2D<int>(width, height);

		// Act & Assert for (0,0)
		const int value00 = 1;
		grid[0, 0] = value00;
		Assert.That(grid[0, 0], Is.EqualTo(value00));

		// Act & Assert for (width-1, height-1)
		var valueWH = 4;
		grid[width - 1, height - 1] = valueWH;
		Assert.That(grid[width - 1, height - 1], Is.EqualTo(valueWH));
	}

	[Test]
	public void Indexer_WhenSettingAndGettingValue_ThenStoresAndRetrievesCorrectly()
	{
		// Arrange
		const int   width     = 5;
		const int   height    = 5;
		var         grid      = new Grid2D<double>(width, height);
		const float testValue = 3.14f;
		const int   testX     = 2;
		const int   testY     = 3;

		// Act
		grid[testX, testY] = testValue;
		var retrievedValue = grid[testX, testY];

		// Assert
		Assert.That(retrievedValue, Is.EqualTo(testValue));
	}

	[TestCase(0,  10, 0)]
	[TestCase(10, 0,  0)]
	[TestCase(-1, 10, 0)]
	[TestCase(10, -5, 0)]
	public void Constructor_WhenCalledWithDefaultValueAndInvalidDimensions_ThenThrowsArgumentException(
		int width,
		int height,
		int defaultValue) =>
		// Act & Assert
		Assert.Throws<ArgumentException>(() => new Grid2D<int>(width, height, defaultValue));

	[TestCase(0,  10)]
	[TestCase(10, 0)]
	[TestCase(-1, 10)]
	[TestCase(10, -5)]
	public void Constructor_WhenDimensionsAreInvalid_ThenThrowsArgumentException(int width, int height) =>
		// Act & Assert
		Assert.Throws<ArgumentException>(() => new Grid2D<int>(width, height));

	[TestCase(5, 5, 5,  0)]  // x out of bounds
	[TestCase(5, 5, 0,  5)]  // y out of bounds
	[TestCase(5, 5, -1, 0)]  // x negative
	[TestCase(5, 5, 0,  -1)] // y negative
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

	[TestCase(5, 5, 5,  0,  10)] // x out of bounds
	[TestCase(5, 5, 0,  5,  10)] // y out of bounds
	[TestCase(5, 5, -1, 0,  10)] // x negative
	[TestCase(5, 5, 0,  -1, 10)] // y negative
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

#endregion
}
