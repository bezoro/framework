using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests;

[TestSubject(typeof(Grid2D<>))]
public class Grid2DUnitTests
{
	public class Constructor
	{
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
			for (var y = 0; y < height; y++)
				grid[x, y].Should().Be(defaultValue);
		}

		[Theory]
		[InlineData(0, 10)]
		[InlineData(10, 0)]
		[InlineData(-1, 10)]
		[InlineData(10, -5)]
		public void WhenDimensionsAreInvalid_ThenThrowsArgumentException(int width, int height)
		{
			// Act & Assert
			var act = () => new Grid2D<int>(width, height);
			act.Should().Throw<ArgumentException>();
		}

		[Theory]
		[InlineData(0, 10, 0)]
		[InlineData(10, 0, 0)]
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

		[Fact]
		public void WhenUsingPooling_ThenDoesNotThrow()
		{
			// Act
			using var grid = new Grid2D<int>(10, 10, usePooling: true);

			// Assert
			grid.Width.Should().Be(10);
			grid.Height.Should().Be(10);
		}
	}

	public class Indexer
	{
		[Fact]
		public void WhenSettingAndGettingValue_ThenStoresAndRetrievesCorrectly()
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
			retrievedValue.Should().Be(testValue);
		}

		[Fact]
		public void WhenSettingAndGettingAtBoundaries_ThenWorksCorrectly()
		{
			// Arrange
			const int width  = 2;
			const int height = 2;
			var       grid   = new Grid2D<int>(width, height);

			// Act & Assert for (0,0)
			const int value00 = 1;
			grid[0, 0] = value00;
			grid[0, 0].Should().Be(value00);

			// Act & Assert for (width-1, height-1)
			const int valueWH = 4;
			grid[width - 1, height - 1] = valueWH;
			grid[width - 1, height - 1].Should().Be(valueWH);
		}

		[Theory]
		[InlineData(5, 5, 5, 0)]   // x out of bounds
		[InlineData(5, 5, 0, 5)]   // y out of bounds
		[InlineData(5, 5, -1, 0)]  // x negative
		[InlineData(5, 5, 0, -1)]  // y negative
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

		[Theory]
		[InlineData(5, 5, 5, 0, 10)]   // x out of bounds
		[InlineData(5, 5, 0, 5, 10)]   // y out of bounds
		[InlineData(5, 5, -1, 0, 10)]  // x negative
		[InlineData(5, 5, 0, -1, 10)]  // y negative
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
			ref var cell = ref grid[1, 1];
			cell = 42;

			// Assert
			grid[1, 1].Should().Be(42);
		}
	}

	public class SpanAccess
	{
		[Fact]
		public void AsSpan_ReturnsCorrectData()
		{
			// Arrange
			var grid = new Grid2D<int>(3, 2);
			grid[0, 0] = 1;
			grid[1, 0] = 2;
			grid[2, 0] = 3;
			grid[0, 1] = 4;
			grid[1, 1] = 5;
			grid[2, 1] = 6;

			// Act
			var span = grid.AsSpan();

			// Assert (row-major order: [1,2,3,4,5,6])
			span.Length.Should().Be(6);
			span[0].Should().Be(1);
			span[1].Should().Be(2);
			span[2].Should().Be(3);
			span[3].Should().Be(4);
			span[4].Should().Be(5);
			span[5].Should().Be(6);
		}

		[Fact]
		public void GetRow_ReturnsCorrectRow()
		{
			// Arrange
			var grid = new Grid2D<int>(3, 2);
			grid[0, 0] = 1;
			grid[1, 0] = 2;
			grid[2, 0] = 3;
			grid[0, 1] = 4;
			grid[1, 1] = 5;
			grid[2, 1] = 6;

			// Act
			var row0 = grid.GetRow(0);
			var row1 = grid.GetRow(1);

			// Assert
			row0.Length.Should().Be(3);
			row0[0].Should().Be(1);
			row0[1].Should().Be(2);
			row0[2].Should().Be(3);

			row1.Length.Should().Be(3);
			row1[0].Should().Be(4);
			row1[1].Should().Be(5);
			row1[2].Should().Be(6);
		}

		[Fact]
		public void GetRow_WhenOutOfBounds_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			var grid = new Grid2D<int>(3, 2);

			// Act & Assert
			Action act = () => _ = grid.GetRow(2);
			act.Should().Throw<ArgumentOutOfRangeException>();
		}
	}

	public class UtilityMethods
	{
		[Fact]
		public void Clear_SetsAllElementsToDefault()
		{
			// Arrange
			var grid = new Grid2D<int>(3, 3, 42);

			// Act
			grid.Clear();

			// Assert
			for (var x = 0; x < 3; x++)
			for (var y = 0; y < 3; y++)
				grid[x, y].Should().Be(0);
		}

		[Fact]
		public void Fill_SetsAllElementsToValue()
		{
			// Arrange
			var grid = new Grid2D<int>(3, 3);

			// Act
			grid.Fill(99);

			// Assert
			for (var x = 0; x < 3; x++)
			for (var y = 0; y < 3; y++)
				grid[x, y].Should().Be(99);
		}

		[Theory]
		[InlineData(0, 0, true)]
		[InlineData(2, 2, true)]
		[InlineData(3, 0, false)]
		[InlineData(0, 3, false)]
		[InlineData(-1, 0, false)]
		[InlineData(0, -1, false)]
		public void IsInBounds_ReturnsCorrectResult(int x, int y, bool expected)
		{
			// Arrange
			var grid = new Grid2D<int>(3, 3);

			// Act & Assert
			grid.IsInBounds(x, y).Should().Be(expected);
		}

		[Fact]
		public void TryGet_WhenInBounds_ReturnsTrueAndValue()
		{
			// Arrange
			var grid = new Grid2D<int>(3, 3);
			grid[1, 1] = 42;

			// Act
			var result = grid.TryGet(1, 1, out var value);

			// Assert
			result.Should().BeTrue();
			value.Should().Be(42);
		}

		[Fact]
		public void TryGet_WhenOutOfBounds_ReturnsFalse()
		{
			// Arrange
			var grid = new Grid2D<int>(3, 3);

			// Act
			var result = grid.TryGet(5, 5, out var value);

			// Assert
			result.Should().BeFalse();
			value.Should().Be(default);
		}

		[Fact]
		public void TrySet_WhenInBounds_ReturnsTrueAndSetsValue()
		{
			// Arrange
			var grid = new Grid2D<int>(3, 3);

			// Act
			var result = grid.TrySet(1, 1, 42);

			// Assert
			result.Should().BeTrue();
			grid[1, 1].Should().Be(42);
		}

		[Fact]
		public void TrySet_WhenOutOfBounds_ReturnsFalse()
		{
			// Arrange
			var grid = new Grid2D<int>(3, 3);

			// Act
			var result = grid.TrySet(5, 5, 42);

			// Assert
			result.Should().BeFalse();
		}
	}

	public class Dispose
	{
		[Fact]
		public void WhenUsingPooling_DisposesWithoutException()
		{
			// Arrange
			var grid = new Grid2D<int>(10, 10, usePooling: true);
			grid[5, 5] = 42;

			// Act & Assert
			var act = () => grid.Dispose();
			act.Should().NotThrow();
		}

		[Fact]
		public void WhenNotUsingPooling_DisposesWithoutException()
		{
			// Arrange
			var grid = new Grid2D<int>(10, 10, usePooling: false);

			// Act & Assert
			var act = () => grid.Dispose();
			act.Should().NotThrow();
		}

		[Fact]
		public void WhenCalledMultipleTimes_DoesNotThrow()
		{
			// Arrange
			var grid = new Grid2D<int>(10, 10, usePooling: true);

			// Act & Assert
			var act = () =>
			{
				grid.Dispose();
				grid.Dispose();
			};
			act.Should().NotThrow();
		}
	}
}
