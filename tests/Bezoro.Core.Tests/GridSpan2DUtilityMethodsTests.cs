using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests;

[TestSubject(typeof(GridSpan2D<>))]
public class GridSpan2DUtilityMethodsTests
{
	[Fact]
	public void AsReadOnly_ReturnsReadOnlyView()
	{
		// Arrange
		Span<int> data = stackalloc int[9];
		var       grid = new GridSpan2D<int>(data, 3, 3);
		grid[1, 1] = 42;

		// Act
		var readOnly = grid.AsReadOnly();

		// Assert
		readOnly.Width.Should().Be(3);
		readOnly.Height.Should().Be(3);
		readOnly[1, 1].Should().Be(42);
	}

	[Fact]
	public void Clear_SetsAllToDefault()
	{
		// Arrange
		Span<int> data = stackalloc int[9];
		var       grid = new GridSpan2D<int>(data, 3, 3);
		grid.Fill(42);

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
	public void Fill_SetsAllToValue()
	{
		// Arrange
		Span<int> data = stackalloc int[9];
		var       grid = new GridSpan2D<int>(data, 3, 3);

		// Act
		grid.Fill(99);

		// Assert
		for (var x = 0; x < 3; x++)
		{
			for (var y = 0; y < 3; y++)
				grid[x, y].Should().Be(99);
		}
	}

	[Fact]
	public void IsInBounds_ReturnsCorrectResult()
	{
		// Arrange
		Span<int> data = stackalloc int[9];
		var       grid = new GridSpan2D<int>(data, 3, 3);

		// Assert
		grid.IsInBounds(0,  0).Should().BeTrue();
		grid.IsInBounds(2,  2).Should().BeTrue();
		grid.IsInBounds(3,  0).Should().BeFalse();
		grid.IsInBounds(-1, 0).Should().BeFalse();
	}
}
