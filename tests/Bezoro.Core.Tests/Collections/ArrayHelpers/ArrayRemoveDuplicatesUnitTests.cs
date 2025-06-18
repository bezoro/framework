using System;
using System.Collections.Generic;
using Xunit;

namespace Bezoro.Core.Tests.Collections.ArrayHelpers;

public class ArrayRemoveDuplicatesUnitTests
{
	public static IEnumerable<object?[]> ObjectDuplicatesAndNullsData =>
		new List<object?[]>
		{
			new object?[] { new object?[] { 1, 2, 2, null, 1, 3, null }, new object[] { 1, 2, 3 } },
			new object?[] { new object?[] { null, null, null }, Array.Empty<object>() },
			new object?[] { new object?[] { "X", "Y", "X", null, "Y", null }, new object[] { "X", "Y" } }
		};
	public static IEnumerable<object[]> IntegerOrStringDuplicatesData =>
		new List<object[]>
		{
			new object[] { new object[] { 1, 1, 1, 1 }, new object[] { 1 } },
			new object[] { new object[] { 1, 2, 3, 3, 4, 5 }, new object[] { 1, 2, 3, 4, 5 } },
			new object[] { new object[] { "A", "A", "B", "B", "C" }, new object[] { "A", "B", "C" } }
		};

	public static IEnumerable<object[]> NumericOrCharDuplicatesData =>
		new List<object[]>
		{
			new object[] { new object[] { 1, 1, 2, 2, 3, 3 }, new object[] { 1, 2, 3 } },
			new object[] { new object[] { 1.1, 1.1, 2.2, 3.3, 3.3 }, new object[] { 1.1, 2.2, 3.3 } },
			new object[] { new object[] { 'a', 'a', 'b', 'c', 'c' }, new object[] { 'a', 'b', 'c' } }
		};

	[Fact]
	public void RemoveDuplicates_WhenArrayContainsCustomObjectDuplicates_ThenUniqueInstancesAreRetained()
	{
		// Arrange
		var array = new[]
		{
			new TestObject(1, "Object1"),
			new TestObject(2, "Object2"),
			new TestObject(1, "Object1"), // Duplicate
			new TestObject(3, "Object3"),
			new TestObject(2, "Object2") // Duplicate
		};

		var expectedArray = new[]
		{
			new TestObject(1, "Object1"),
			new TestObject(2, "Object2"),
			new TestObject(3, "Object3")
		};

		// Act
		Common.Helpers.ArrayHelpers.RemoveDuplicates(ref array);

		// Assert
		Assert.Equal(expectedArray, array);
	}

	[Theory]
	[MemberData(nameof(IntegerOrStringDuplicatesData))]
	public void RemoveDuplicates_WhenArrayContainsIntegerOrStringDuplicates_ThenUniqueValuesAreRetained(
		object[] array,
		object[] expectedArray)
	{
		// Act
		Common.Helpers.ArrayHelpers.RemoveDuplicates(ref array);

		// Assert
		Assert.Equal(expectedArray, array);
	}

	[Theory]
	[MemberData(nameof(NumericOrCharDuplicatesData))]
	public void RemoveDuplicates_WhenArrayContainsNumericOrCharDuplicates_ThenUniqueValuesAreRetained(
		object[] array,
		object[] expectedArray)
	{
		// Act
		Common.Helpers.ArrayHelpers.RemoveDuplicates(ref array);

		// Assert
		Assert.Equal(expectedArray, array);
	}

	[Theory]
	[MemberData(nameof(ObjectDuplicatesAndNullsData))]
	public void
		RemoveDuplicates_WhenArrayContainsObjectDuplicatesAndNulls_ThenUniqueValuesAreRetainedAndNullsAreRemoved(
			object?[] array,
			object[] expected)
	{
		// Act
		Common.Helpers.ArrayHelpers.RemoveDuplicates(ref array);

		// Assert
		Assert.Equal(expected, array);
	}

	[Fact]
	public void RemoveDuplicates_WhenArrayContainsSingleElement_ThenItRemainsUnchanged()
	{
		// Arrange
		var array = new[] { 42 };

		// Act
		Common.Helpers.ArrayHelpers.RemoveDuplicates(ref array);

		// Assert
		Assert.Equal(new[] { 42 }, array);
	}

	[Fact]
	public void RemoveDuplicates_WhenArrayIsEmpty_ThenItRemainsUnchanged()
	{
		// Arrange
		int[] array = Array.Empty<int>();

		// Act
		Common.Helpers.ArrayHelpers.RemoveDuplicates(ref array);

		// Assert
		Assert.Empty(array);
	}

	[Fact]
	public void RemoveDuplicates_WhenInputArrayIsNull_ThenItRemainsNull()
	{
		// Arrange
		int[]? array = null;

		// Act
		Common.Helpers.ArrayHelpers.RemoveDuplicates(ref array);

		// Assert
		Assert.Null(array);
	}

	private sealed class TestObject
	{
		private int    Id   { get; }
		private string Name { get; }

		#region Equality

		public override bool Equals(object? obj) =>
			obj is TestObject other && Id == other.Id && Name == other.Name;

		public override int GetHashCode() =>
			HashCode.Combine(Id, Name);

		#endregion

		public TestObject(int id, string name)
		{
			Id   = id;
			Name = name;
		}
	}
}
