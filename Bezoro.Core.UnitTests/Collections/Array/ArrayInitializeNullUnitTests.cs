using System.Collections;
using Bezoro.Core.Collections.Array;
using NUnit.Framework;

namespace Bezoro.Core.UnitTests.Collections.Array;

[TestFixture]
[TestOf(typeof(ArrayHelpers))]
public class ArrayInitializeNullUnitTests
{
	private static IEnumerable InputArrayReturnsExpectedTestCases
	{
		get
		{
			yield return new TestCaseData(null, new int[0]).SetName(
				"InitializeNullArray_NullInput_InitializesToEmpty");

			yield return new TestCaseData(new[] { 1, 2, 3 }, new[] { 1, 2, 3 }).SetName(
				"InitializeNullArray_NonNullInput_PreservesOriginal");

			yield return new TestCaseData(new int[0], new int[0]).SetName(
				"InitializeNullArray_EmptyInput_PreservesEmpty");
		}
	}

#region Test Methods

	[Test]
	public void InitializeNullArray_WhenInitializingArrays_ThenReferenceChangesOnlyForNullArrays()
	{
		// Arrange
		int[] nullArray    = null;
		var   nonNullArray = new[] { 1, 2, 3 };

		var originalNonNullReference = nonNullArray;

		// Act
		ArrayHelpers.InitializeNullArray(ref nullArray);
		ArrayHelpers.InitializeNullArray(ref nonNullArray);

		// Assert
		Assert.That(nullArray, Is.Not.Null, "Null array should be initialized");
		Assert.That(
			nonNullArray, Is.SameAs(originalNonNullReference),
			"Non-null array reference should not change");
	}

	[Test]
	public void InitializeNullArray_WhenInputIsEmpty_ThenRemainsEmpty()
	{
		// Arrange
		var input = new int[0];

		// Act
		ArrayHelpers.InitializeNullArray(ref input);

		// Assert
		Assert.That(input, Is.Not.Null, "Array should remain non-null");
		Assert.That(input, Is.Empty,    "Array should remain empty");
	}

	[Test]
	public void InitializeNullArray_WhenInputIsNotNull_ThenContentsRemainUnchanged()
	{
		// Arrange
		var input    = new[] { 1, 2, 3 };
		var expected = new[] { 1, 2, 3 };

		// Act
		ArrayHelpers.InitializeNullArray(ref input);

		// Assert
		Assert.That(input, Is.EqualTo(expected));
	}

	[Test]
	public void InitializeNullArray_WhenInputIsNull_ThenInitializesToEmptyArray()
	{
		// Arrange
		int[] input = null;

		// Act
		ArrayHelpers.InitializeNullArray(ref input);

		// Assert
		Assert.That(input, Is.Not.Null, "Array should be initialized to non-null");
		Assert.That(input, Is.Empty,    "Array should be empty");
	}

	[TestCaseSource(nameof(InputArrayReturnsExpectedTestCases))]
	public void InitializeNullArray_WhenInputArrayProvided_ThenReturnsExpectedResult(int[] input, int[] expected)
	{
		// Act
		ArrayHelpers.InitializeNullArray(ref input);

		// Assert
		Assert.That(input, Is.EqualTo(expected));
	}

#endregion
}
