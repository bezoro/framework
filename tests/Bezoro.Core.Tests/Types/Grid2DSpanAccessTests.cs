using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Grid2D<>))]
public class Grid2DSpanAccessTests
{
	[Fact]
	public void AsSpan_WhenCalled_ShouldReturnCorrectData()
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
	public void GetRow_WhenCalled_ShouldReturnCorrectRow()
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
	public void GetRowWhenOutOfBounds_WhenCalled_ShouldThrowArgumentOutOfRangeException()
	{
		// Arrange
		var grid = new Grid2D<int>(3, 2);

		// Act & Assert
		Action act = () => _ = grid.GetRow(2);
		act.Should().Throw<ArgumentOutOfRangeException>();
	}
}
