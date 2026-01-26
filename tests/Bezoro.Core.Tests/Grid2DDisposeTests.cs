using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests;

[TestSubject(typeof(Grid2D<>))]
public class Grid2DDisposeTests
{
	[Fact]
	public void WhenCalledMultipleTimes_DoesNotThrow()
	{
		// Arrange
		var grid = new Grid2D<int>(10, 10, true);

		// Act & Assert
		var act = () =>
		{
			grid.Dispose();
			grid.Dispose();
		};

		act.Should().NotThrow();
	}

	[Fact]
	public void WhenNotUsingPooling_DisposesWithoutException()
	{
		// Arrange
		var grid = new Grid2D<int>(10, 10);

		// Act & Assert
		var act = () => grid.Dispose();
		act.Should().NotThrow();
	}

	[Fact]
	public void WhenUsingPooling_DisposesWithoutException()
	{
		// Arrange
		var grid = new Grid2D<int>(10, 10, true);
		grid[5, 5] = 42;

		// Act & Assert
		var act = () => grid.Dispose();
		act.Should().NotThrow();
	}
}
