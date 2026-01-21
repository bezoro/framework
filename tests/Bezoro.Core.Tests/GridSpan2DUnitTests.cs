using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests;

[TestSubject(typeof(GridSpan2D<>))]
public class GridSpan2DUnitTests
{
	public class Constructor
	{
		[Fact]
		public void WhenCreatedFromSpan_ThenInitializesCorrectly()
		{
			// Arrange
			Span<int> data = stackalloc int[12];
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

	public class Indexer
	{
		[Fact]
		public void WhenSettingAndGetting_ThenWorksCorrectly()
		{
			// Arrange
			Span<int> data = stackalloc int[9];
			var grid = new GridSpan2D<int>(data, 3, 3);

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
			var grid = new GridSpan2D<int>(data, 3, 3);
			grid[1, 1] = 10;

			// Act
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
			Span<int> data = stackalloc int[6];
			var grid = new GridSpan2D<int>(data, 3, 2);
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
		public void GetRow_ReturnsCorrectRow()
		{
			// Arrange
			Span<int> data = stackalloc int[6];
			var grid = new GridSpan2D<int>(data, 3, 2);
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

	public class UtilityMethods
	{
		[Fact]
		public void IsInBounds_ReturnsCorrectResult()
		{
			// Arrange
			Span<int> data = stackalloc int[9];
			var grid = new GridSpan2D<int>(data, 3, 3);

			// Assert
			grid.IsInBounds(0, 0).Should().BeTrue();
			grid.IsInBounds(2, 2).Should().BeTrue();
			grid.IsInBounds(3, 0).Should().BeFalse();
			grid.IsInBounds(-1, 0).Should().BeFalse();
		}

		[Fact]
		public void Clear_SetsAllToDefault()
		{
			// Arrange
			Span<int> data = stackalloc int[9];
			var grid = new GridSpan2D<int>(data, 3, 3);
			grid.Fill(42);

			// Act
			grid.Clear();

			// Assert
			for (var x = 0; x < 3; x++)
			for (var y = 0; y < 3; y++)
				grid[x, y].Should().Be(0);
		}

		[Fact]
		public void Fill_SetsAllToValue()
		{
			// Arrange
			Span<int> data = stackalloc int[9];
			var grid = new GridSpan2D<int>(data, 3, 3);

			// Act
			grid.Fill(99);

			// Assert
			for (var x = 0; x < 3; x++)
			for (var y = 0; y < 3; y++)
				grid[x, y].Should().Be(99);
		}

		[Fact]
		public void AsReadOnly_ReturnsReadOnlyView()
		{
			// Arrange
			Span<int> data = stackalloc int[9];
			var grid = new GridSpan2D<int>(data, 3, 3);
			grid[1, 1] = 42;

			// Act
			var readOnly = grid.AsReadOnly();

			// Assert
			readOnly.Width.Should().Be(3);
			readOnly.Height.Should().Be(3);
			readOnly[1, 1].Should().Be(42);
		}
	}

	public class FromGrid2D
	{
		[Fact]
		public void AsGridSpan_CreatesValidSpanView()
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
		public void AsReadOnlyGridSpan_CreatesValidReadOnlyView()
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
}

[TestSubject(typeof(ReadOnlyGridSpan2D<>))]
public class ReadOnlyGridSpan2DUnitTests
{
	[Fact]
	public void WhenCreated_ThenCanReadButNotWrite()
	{
		// Arrange
		ReadOnlySpan<int> data = stackalloc int[] { 1, 2, 3, 4, 5, 6 };
		var grid = new ReadOnlyGridSpan2D<int>(data, 3, 2);

		// Assert
		grid.Width.Should().Be(3);
		grid.Height.Should().Be(2);
		grid[0, 0].Should().Be(1);
		grid[2, 1].Should().Be(6);
	}

	[Fact]
	public void GetRow_ReturnsCorrectReadOnlyRow()
	{
		// Arrange
		ReadOnlySpan<int> data = stackalloc int[] { 1, 2, 3, 4, 5, 6 };
		var grid = new ReadOnlyGridSpan2D<int>(data, 3, 2);

		// Act
		var row1 = grid.GetRow(1);

		// Assert
		row1.Length.Should().Be(3);
		row1[0].Should().Be(4);
		row1[1].Should().Be(5);
		row1[2].Should().Be(6);
	}

	[Fact]
	public void IsInBounds_ReturnsCorrectResult()
	{
		// Arrange
		ReadOnlySpan<int> data = stackalloc int[9];
		var grid = new ReadOnlyGridSpan2D<int>(data, 3, 3);

		// Assert
		grid.IsInBounds(0, 0).Should().BeTrue();
		grid.IsInBounds(2, 2).Should().BeTrue();
		grid.IsInBounds(3, 0).Should().BeFalse();
	}
}
