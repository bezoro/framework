using System;
using System.Collections.Generic;
using Xunit;

namespace Bezoro.Core.Tests.Collections.ArrayHelpers;

public class ArrayCompareUnitTests
{
	public static IEnumerable<object?[]> DifferentElementsData =>
		new List<object?[]>
		{
			new object?[] { new object[] { 1, 2, 3 }, new object[] { 1, 2, 4 } },
			new object?[] { new object[] { "a", "b", "c" }, new object[] { "a", "b", "d" } },
			new object?[] { Array.Empty<object>(), new object[] { 0 } }
		};

	public static IEnumerable<object?[]> OneNullArrayData =>
		new List<object?[]>
		{
			new object?[] { new object[] { 1, 2, 3 }, null },
			new object?[] { null, new object[] { 1, 2, 3 } }
		};

	public static IEnumerable<object?[]> SimpleEqualArraysData =>
		new List<object?[]>
		{
			new object?[] { new object[] { 1.1, 2.2, 3.3 }, new object[] { 1.1, 2.2, 3.3 } },
			new object?[] { new object[] { true, false }, new object[] { true, false } }
		};

	[Fact]
	public void CompareArrays_WhenArraysContainEqualCustomObjects_ReturnsTrue()
	{
		// Arrange
		var obj1 = new TestObject(1, "Test1");
		var obj2 = new TestObject(2, "Test2");
		var obj3 = new TestObject(1, "Test1"); // Equal to obj1 by value

		var array1 = new[] { obj1, obj2 };
		var array2 = new[] { obj1, obj2 }; // Identical instance and values to array1
		var array3 = new[] { obj3, obj2 }; // Different instance for first element, but value-equal to array1

		// Act
		var result1 = Common.Helpers.ArrayHelpers.CompareArrays(array1, array2); // array1 vs array2 (identical)
		var result2 = Common.Helpers.ArrayHelpers.CompareArrays(array1, array3);                // array1 vs array3 (value-equal)

		// Assert
		Assert.True(result1, "Comparing an array to itself or an identical array should return true.");
		Assert.True(result2, "Comparing arrays with element-wise equal custom objects should return true.");
	}

	[Theory]
	[MemberData(nameof(DifferentElementsData))]
	public void CompareArrays_WhenArraysHaveDifferentElements_ReturnsFalse(object[]? a, object[]? b)
	{
		// Act
		var comparisonResult = Common.Helpers.ArrayHelpers.CompareArrays(a, b);

		// Assert
		Assert.False(
			comparisonResult,
			"Comparing 2 arrays with different elements should be considered unequal."
		);
	}

	[Fact]
	public void CompareArrays_WhenArraysHaveDifferentLengths_ReturnsFalse()
	{
		// Arrange
		var array1 = new[] { 1, 2, 3 };
		var array2 = new[] { 1, 2, 3, 4 };

		// Act
		var result = Common.Helpers.ArrayHelpers.CompareArrays(array1, array2);

		// Assert
		Assert.False(result, "Comparing 2 arrays with different lengths should be considered unequal.");
	}

	[Theory]
	[MemberData(nameof(SimpleEqualArraysData))]
	public void CompareArrays_WhenArraysOfSimpleTypesAreEqual_ReturnsTrue(object[]? a, object[]? b)
	{
		// Act
		var result = Common.Helpers.ArrayHelpers.CompareArrays(a, b);

		// Assert
		Assert.True(
			result,
			"Comparing 2 arrays with the same simple type elements should be considered equal."
		);
	}

	[Fact]
	public void CompareArrays_WhenBothArraysAreNull_ReturnsTrue()
	{
		// Act
		var result = Common.Helpers.ArrayHelpers.CompareArrays<object>(null, null);

		// Assert
		Assert.True(result, "Comparing 2 null arrays should be considered equal.");
	}

	[Fact]
	public void CompareArrays_WhenBothArraysContainOnlyNullsAndSameLength_ReturnsTrue()
	{
		// Arrange
		string?[] array1 = { null, null, null };
		string?[] array2 = { null, null, null };

		// Act
		var result = Common.Helpers.ArrayHelpers.CompareArrays(array1, array2);

		// Assert
		Assert.True(
			result,
			"Comparing 2 arrays of the same length with only null elements should be considered equal."
		);
	}

	[Theory]
	[MemberData(nameof(OneNullArrayData))]
	public void CompareArrays_WhenOneArrayIsNullAndOtherIsNot_ReturnsFalse(object[]? a, object[]? b)
	{
		// Act
		var result = Common.Helpers.ArrayHelpers.CompareArrays(a, b);

		// Assert
		Assert.False(result, "Comparing a null array with a valid array should return false.");
	}

	[Fact]
	public void CompareArrays_WithMixedNullElements_ReturnsTrueForIdenticalAndFalseForDifferent()
	{
		// Arrange
		string?[] array1Identical = { "a", null, "c" };
		string?[] array2Identical = { "a", null, "c" };
		string?[] array3Different = { "a", null, "b" };

		// Act
		var resultForIdentical = Common.Helpers.ArrayHelpers.CompareArrays(array1Identical, array2Identical);
		var resultForDifferent = Common.Helpers.ArrayHelpers.CompareArrays(array1Identical, array3Different);

		// Assert
		Assert.True(resultForIdentical, "Identical arrays with null elements should return true.");
		Assert.False(resultForDifferent, "Different arrays with null elements should return false.");
	}

	private class TestObject
	{
		public TestObject(int id, string name)
		{
			Id   = id;
			Name = name;
		}

		private int    Id   { get; }
		private string Name { get; }

		public override bool Equals(object? obj) =>
			obj is TestObject other && Id == other.Id && Name == other.Name;

		public override int GetHashCode() =>
			HashCode.Combine(Id, Name);
	}
}
