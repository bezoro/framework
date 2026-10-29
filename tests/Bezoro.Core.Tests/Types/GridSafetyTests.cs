using System;
using Bezoro.Core.Types;
using Xunit;

namespace Bezoro.Core.Tests.Types;

public class GridSafetyTests
{
	[Fact]
	public void Constructor_RejectsDimensionOverflow()
	{
		Assert.Throws<OverflowException>(() => new Grid2D<int>(65536, 65536));
		Assert.Throws<OverflowException>(() => new GridSpan2D<int>(new int[1], 65536, 65536));
		Assert.Throws<OverflowException>(() => new ReadOnlyGridSpan2D<int>(new int[1], 65536, 65536));
	}

	[Theory]
	[InlineData(0, 2)]
	[InlineData(2, 0)]
	[InlineData(-1, 2)]
	[InlineData(2, -1)]
	public void SpanConstructors_RejectInvalidDimensions(int width, int height)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new GridSpan2D<int>(new int[4], width, height));
		Assert.Throws<ArgumentOutOfRangeException>(() => new ReadOnlyGridSpan2D<int>(new int[4], width, height));
	}

	[Fact]
	public void SpanConstructors_RejectUndersizedStorage()
	{
		Assert.Throws<ArgumentException>(() => new GridSpan2D<int>(new int[3], 2, 2));
		Assert.Throws<ArgumentException>(() => new ReadOnlyGridSpan2D<int>(new int[3], 2, 2));
	}

	[Theory]
	[InlineData(2, 0)]
	[InlineData(-1, 1)]
	[InlineData(0, 2)]
	[InlineData(0, -1)]
	public void SpanIndexers_RejectCoordinatesThatAliasOtherRows(int x, int y)
	{
		var data = new[] { 1, 2, 3, 4, 5, 6 };
		Assert.Throws<IndexOutOfRangeException>(() => ReadMutable(data, x, y));
		Assert.Throws<IndexOutOfRangeException>(() => ReadOnly(data, x, y));
		Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, data);
	}

	[Fact]
	public void PooledConstructor_StartsWithDefaultValues()
	{
		using (var previous = new Grid2D<int>(4, 4, usePooling: true))
			previous.Fill(42);

		using var current = new Grid2D<int>(4, 4, usePooling: true);
		Assert.Equal(new int[16], current.AsSpan().ToArray());
	}

	[Fact]
	public void DisposedGrid_RejectsAllStorageAccess()
	{
		var grid = new Grid2D<int>(2, 2, usePooling: true);
		grid.Dispose();
		Assert.Throws<ObjectDisposedException>(() => grid[0, 0]);
		Assert.Throws<ObjectDisposedException>(() => grid.AsSpan());
		Assert.Throws<ObjectDisposedException>(() => grid.AsReadOnlySpan());
		Assert.Throws<ObjectDisposedException>(() => grid.GetRow(0));
		Assert.Throws<ObjectDisposedException>(() => grid.GetRowReadOnly(0));
		Assert.Throws<ObjectDisposedException>(() => grid.TryGet(0, 0, out _));
		Assert.Throws<ObjectDisposedException>(() => grid.TrySet(0, 0, 1));
		Assert.Throws<ObjectDisposedException>(() => grid.AsGridSpan());
		Assert.Throws<ObjectDisposedException>(() => grid.AsReadOnlyGridSpan());
	}

	private static int ReadMutable(int[] data, int x, int y) => new GridSpan2D<int>(data, 2, 2)[x, y];
	private static int ReadOnly(int[] data, int x, int y) => new ReadOnlyGridSpan2D<int>(data, 2, 2)[x, y];
}
