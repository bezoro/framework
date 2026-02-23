using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Grid2D<>))]
public class Grid2DUtilityMethodsTests
{
	[Fact]
	public void Grid2DUtilityMethods_WhenCalled_ShouldClear_SetsAllElementsToDefault()
	{
		// Arrange
		var grid = new Grid2D<int>(3, 3, 42);

		// Act
		grid.Clear();

		// Assert
		for (var x = 0; x < 3; x++)
		{
			for (var y = 0; y < 3; y++)
				grid[x, y].Should().Be(0);
		}
	}

	[Fact]
	public void Grid2DUtilityMethods_WhenCalled_ShouldFill_SetsAllElementsToValue()
	{
		// Arrange
		var grid = new Grid2D<int>(3, 3);

		// Act
		grid.Fill(99);

		// Assert
		for (var x = 0; x < 3; x++)
		{
			for (var y = 0; y < 3; y++)
				grid[x, y].Should().Be(99);
		}
	}

	[Theory]
	[InlineData(0,  0,  true)]
	[InlineData(2,  2,  true)]
	[InlineData(3,  0,  false)]
	[InlineData(0,  3,  false)]
	[InlineData(-1, 0,  false)]
	[InlineData(0,  -1, false)]
	public void IsInBounds_WhenCalled_ShouldReturnCorrectResult(int x, int y, bool expected)
	{
		// Arrange
		var grid = new Grid2D<int>(3, 3);

		// Act & Assert
		grid.IsInBounds(x, y).Should().Be(expected);
	}

	[Fact]
	public void TryGetWhenInBounds_WhenCalled_ShouldReturnTrueAndValue()
	{
		// Arrange
		var grid = new Grid2D<int>(3, 3);
		grid[1, 1] = 42;

		// Act
		bool result = grid.TryGet(1, 1, out int value);

		// Assert
		result.Should().BeTrue();
		value.Should().Be(42);
	}

	[Fact]
	public void TryGetWhenOutOfBounds_WhenCalled_ShouldReturnFalse()
	{
		// Arrange
		var grid = new Grid2D<int>(3, 3);

		// Act
		bool result = grid.TryGet(5, 5, out int value);

		// Assert
		result.Should().BeFalse();
		value.Should().Be(default);
	}

	[Fact]
	public void TrySetWhenInBounds_WhenCalled_ShouldReturnTrueAndSetsValue()
	{
		// Arrange
		var grid = new Grid2D<int>(3, 3);

		// Act
		bool result = grid.TrySet(1, 1, 42);

		// Assert
		result.Should().BeTrue();
		grid[1, 1].Should().Be(42);
	}

	[Fact]
	public void TrySetWhenOutOfBounds_WhenCalled_ShouldReturnFalse()
	{
		// Arrange
		var grid = new Grid2D<int>(3, 3);

		// Act
		bool result = grid.TrySet(5, 5, 42);

		// Assert
		result.Should().BeFalse();
	}
}
