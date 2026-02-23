using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(GridSpan2D<>))]
public class GridSpan2DSpanAccessTests
{
	[Fact]
	public void AsSpan_WhenCalled_ShouldReturnCorrectData()
	{
		// Arrange
		Span<int> data = stackalloc int[6];
		var       grid = new GridSpan2D<int>(data, 3, 2);
		grid[0, 0] = 1;
		grid[1, 0] = 2;
		grid[2, 0] = 3;
		grid[0, 1] = 4;
		grid[1, 1] = 5;
		grid[2, 1] = 6;

		// Act
		var span = grid.AsSpan();

		// Assert
		span.Length.Should().Be(6);
		span[0].Should().Be(1);
		span[5].Should().Be(6);
	}

	[Fact]
	public void GetRow_WhenCalled_ShouldReturnCorrectRow()
	{
		// Arrange
		Span<int> data = stackalloc int[6];
		var       grid = new GridSpan2D<int>(data, 3, 2);
		grid[0, 0] = 1;
		grid[1, 0] = 2;
		grid[2, 0] = 3;
		grid[0, 1] = 4;
		grid[1, 1] = 5;
		grid[2, 1] = 6;

		// Act
		var row1 = grid.GetRow(1);

		// Assert
		row1.Length.Should().Be(3);
		row1[0].Should().Be(4);
		row1[1].Should().Be(5);
		row1[2].Should().Be(6);
	}
}

