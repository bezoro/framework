using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(GridSpan2D<>))]
public class GridSpan2DConstructorTests
{
	[Fact]
	public void GridSpan2DConstructor_WhenCreatedFromSpan_ShouldInitializesCorrectly()
	{
		// Arrange
		Span<int> data   = stackalloc int[12];
		const int WIDTH  = 3;
		const int HEIGHT = 4;

		// Act
		var grid = new GridSpan2D<int>(data, WIDTH, HEIGHT);

		// Assert
		grid.Width.Should().Be(WIDTH);
		grid.Height.Should().Be(HEIGHT);
		grid.Length.Should().Be(WIDTH * HEIGHT);
	}
}
