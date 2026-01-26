using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests;

[TestSubject(typeof(GridSpan2D<>))]
public class GridSpan2DConstructorTests
{
	[Fact]
	public void WhenCreatedFromSpan_ThenInitializesCorrectly()
	{
		// Arrange
		Span<int> data   = stackalloc int[12];
		const int width  = 3;
		const int height = 4;

		// Act
		var grid = new GridSpan2D<int>(data, width, height);

		// Assert
		grid.Width.Should().Be(width);
		grid.Height.Should().Be(height);
		grid.Length.Should().Be(width * height);
	}
}
