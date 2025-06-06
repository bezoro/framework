using System;
using NUnit.Framework;

namespace Bezoro.Core.UnitTests;

[TestFixture]
public class Grid2DTests
{
#region Test Methods

	[Test]
	public void GenericGrid2D_Constructor_ValidDimensions_InitializesDataArray()
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
	public void GenericGrid2D_Constructor_WithDefaultValue_InitializesAllCells()
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
	public void GenericGrid2D_Indexer_SetAndGetAtBoundaries_WorksCorrectly()
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
	public void GenericGrid2D_Indexer_SetAndGetValue_StoresAndRetrievesCorrectly()
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

	[Test]
	public void Grid2D_Constructor_ValidDimensions_PropertiesSetCorrectly()
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

	[TestCase(0,  10)]
	[TestCase(10, 0)]
	[TestCase(-1, 10)]
	[TestCase(10, -5)]
	public void GenericGrid2D_Constructor_InvalidDimensions_ThrowsArgumentException(int width, int height) =>
		// Act & Assert
		Assert.Throws<ArgumentException>(() => new Grid2D<int>(width, height));

	[TestCase(0,  10, 0)]
	[TestCase(10, 0,  0)]
	[TestCase(-1, 10, 0)]
	[TestCase(10, -5, 0)]
	public void GenericGrid2D_Constructor_WithDefaultValue_InvalidDimensions_ThrowsArgumentException(
		int width,
		int height,
		int defaultValue) =>
		// Act & Assert
		Assert.Throws<ArgumentException>(() => new Grid2D<int>(width, height, defaultValue));

	[TestCase(5, 5, 5,  0)]  // x out of bounds
	[TestCase(5, 5, 0,  5)]  // y out of bounds
	[TestCase(5, 5, -1, 0)]  // x negative
	[TestCase(5, 5, 0,  -1)] // y negative
	public void GenericGrid2D_Indexer_AccessOutOfBounds_ThrowsIndexOutOfRangeException(
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
	public void GenericGrid2D_Indexer_SetOutOfBounds_ThrowsIndexOutOfRangeException(
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

	[TestCase(0,  10)]
	[TestCase(10, 0)]
	[TestCase(-1, 10)]
	[TestCase(10, -5)]
	public void Grid2D_Constructor_InvalidDimensions_ThrowsArgumentException(int width, int height) =>
		// Act & Assert
		Assert.Throws<ArgumentException>(() => new Grid2D(width, height));

#endregion
}
