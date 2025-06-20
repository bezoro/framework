using System;
using Xunit;

namespace Bezoro.Core.Tests.Collections.ArrayHelpers;

public class ArrayMergeUnitTests
{
	[Fact]
	public void Merge_WhenBothSourceAndDestinationArraysAreNull_ThenDestinationRemainsNull()
	{
		// Arrange
		string?[]? to = null;

		// Act
		Common.Helpers.ArrayHelpers.Merge(null, ref to);

		// Assert
		Assert.Null(to);
	}

	[Fact]
	public void Merge_WhenCustomObjectArrayAndDestinationSmaller_ThenDestinationResizedAndAllElementsCopied()
	{
		// Arrange
		var from = new[]
		{
			new TestObject(1, "Object1"),
			new TestObject(2, "Object2"),
			new TestObject(3, "Object3")
		};

		var to = new TestObject[2]; // Smaller array than 'from'

		// Act
		Common.Helpers.ArrayHelpers.Merge(from, ref to);

		// Assert
		Assert.Equal(from, to);
	}

	[Fact]
	public void
		Merge_WhenDestinationArrayContainsPartialNulls_ThenNullsInDestinationReplacedByCorrespondingSourceElements()
	{
		// Arrange
		var from = new[] { "A", "B", "C" };
		var to   = new[] { "X", null, "Z" };

		// Act
		Common.Helpers.ArrayHelpers.Merge(from, ref to);

		// Assert
		Assert.Equal(new[] { "X", "B", "Z" }, to);
	}

	[Fact]
	public void
		Merge_WhenDestinationArrayHasExistingDataAndNulls_ThenOnlyNullsInDestinationReplacedBySourceElements()
	{
		// Arrange
		var from = new[] { "X", "Y", "Z" };
		var to   = new[] { "A", "B", null };

		// Act
		Common.Helpers.ArrayHelpers.Merge(from, ref to);

		// Assert
		Assert.Equal(new[] { "A", "B", "Z" }, to);
	}

	[Fact]
	public void Merge_WhenDestinationArrayIsEmpty_ThenDestinationResizedAndAllSourceElementsCopied()
	{
		// Arrange
		var      from = new[] { "A", "B", "C" };
		string[] to   = Array.Empty<string>();

		// Act
		Common.Helpers.ArrayHelpers.Merge(from, ref to);

		// Assert
		Assert.Equal(from, to);
	}

	[Fact]
	public void Merge_WhenDestinationArrayIsNull_ThenDestinationInitializedAndAllSourceElementsCopied()
	{
		// Arrange
		var        from = new[] { "A", "B", "C" };
		string?[]? to   = null;

		// Act
		Common.Helpers.ArrayHelpers.Merge(from, ref to);

		// Assert
		Assert.Equal(from, to);
	}

	[Fact]
	public void Merge_WhenDestinationArrayLargerAndContainsElements_ThenDestinationArrayUnchanged()
	{
		// Arrange
		var from = new[] { 1, 2 };
		var to   = new[] { 3, 4, 5 };

		// Act
		Common.Helpers.ArrayHelpers.Merge(from, ref to);

		// Assert
		Assert.Equal(new[] { 3, 4, 5 }, to);
	}

	[Fact]
	public void Merge_WhenSourceArrayIsEmpty_ThenDestinationArrayUnchanged()
	{
		// Arrange
		string[] from = Array.Empty<string>();
		var      to   = new[] { "Existing" };

		// Act
		Common.Helpers.ArrayHelpers.Merge(from, ref to);

		// Assert
		Assert.Equal(new[] { "Existing" }, to);
	}

	[Fact]
	public void Merge_WhenSourceArrayIsNull_ThenDestinationArrayUnchanged()
	{
		// Arrange
		string[] to = { "Existing" };

		// Act
		Common.Helpers.ArrayHelpers.Merge(null, ref to);

		// Assert
		Assert.Equal(new[] { "Existing" }, to);
	}

	[Fact]
	public void Merge_WhenSourceArrayLargerThanDestination_ThenDestinationResizedAndAllElementsCopied()
	{
		// Arrange
		var from = new[] { 1, 2, 3 };
		var to   = new int[2]; // Smaller array than 'from'

		// Act
		Common.Helpers.ArrayHelpers.Merge(from, ref to);

		// Assert
		Assert.Equal(new[] { 1, 2, 3 }, to);
	}

	[Fact]
	public void Merge_WhenSourceArrayLargerThanDestination_ThenDestinationResizedToFitSource()
	{
		// Arrange
		var from = new[] { 100, 200, 300 };
		var to   = new int[1]; // Smaller array than 'from'

		// Act
		Common.Helpers.ArrayHelpers.Merge(from, ref to);

		// Assert
		Assert.Equal(from, to);
	}

	// Test class for complex type checking
	private sealed class TestObject
	{
		public TestObject(int id, string name)
		{
			Id   = id;
			Name = name;
		}

		private int    Id   { get; }
		private string Name { get; }

		#region Equality

		public override bool Equals(object? obj) =>
			obj is TestObject other && Id == other.Id && Name == other.Name;

		public override int GetHashCode() =>
			HashCode.Combine(Id, Name);

		#endregion
	}
}
