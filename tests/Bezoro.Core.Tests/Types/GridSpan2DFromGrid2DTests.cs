using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(GridSpan2D<>))]
public class GridSpan2DFromGrid2DTests
{
	[Fact]
	public void GridSpan2DFromGrid2D_WhenCalled_ShouldAsGridSpan_CreatesValidSpanView()
	{
		// Arrange
		var grid = new Grid2D<int>(3, 3);
		grid[1, 1] = 42;

		// Act
		var span = grid.AsGridSpan();

		// Assert
		span.Width.Should().Be(3);
		span.Height.Should().Be(3);
		span[1, 1].Should().Be(42);

		// Modify through span
		span[2, 2] = 99;
		grid[2, 2].Should().Be(99);
	}

	[Fact]
	public void GridSpan2DFromGrid2D_WhenCalled_ShouldAsReadOnlyGridSpan_CreatesValidReadOnlyView()
	{
		// Arrange
		var grid = new Grid2D<int>(3, 3);
		grid[1, 1] = 42;

		// Act
		var readOnly = grid.AsReadOnlyGridSpan();

		// Assert
		readOnly.Width.Should().Be(3);
		readOnly.Height.Should().Be(3);
		readOnly[1, 1].Should().Be(42);
	}
}

