using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests;

[TestSubject(typeof(GridSpan2D<>))]
public class GridSpan2DIndexerTests
{
	[Fact]
	public void WhenSettingAndGetting_ThenWorksCorrectly()
	{
		// Arrange
		Span<int> data = stackalloc int[9];
		var       grid = new GridSpan2D<int>(data, 3, 3);

		// Act
		grid[1, 2] = 42;

		// Assert
		grid[1, 2].Should().Be(42);
	}

	[Fact]
	public void WhenUsingRefReturn_ThenCanModifyInPlace()
	{
		// Arrange
		Span<int> data = stackalloc int[9];
		var       grid = new GridSpan2D<int>(data, 3, 3);
		grid[1, 1] = 10;

		// Act
		ref int cell = ref grid[1, 1];
		cell = 42;

		// Assert
		grid[1, 1].Should().Be(42);
	}
}
