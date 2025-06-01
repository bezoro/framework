using System.Collections;
using Bezoro.Core.Collections.Array;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Array
{
	[TestFixture]
	[TestOf(typeof(ArrayHelpers))]
	public class InitializeNullArray
	{
		private static IEnumerable _InputArrayReturnsExpectedTestCases
		{
			get
			{
				yield return new TestCaseData(null, new int[0]).SetName(
					"InitializeNullArray_NullInput_InitializesToEmpty");

				yield return new TestCaseData(new[] { 1, 2, 3 }, new[] { 1, 2, 3 }).SetName(
					"InitializeNullArray_NonNullInput_PreservesOriginal");
			}
		}

		[TestCaseSource(nameof(_InputArrayReturnsExpectedTestCases))]
		public void InputArray_ReturnsExpected(int[] input, int[] expected)
		{
			// Act
			ArrayHelpers.InitializeNullArray(ref input);

			// Assert
			Assert.That(input, Is.EqualTo(expected));
		}

		[Test]
		public void NonNullInput_DoesNotAlterContents()
		{
			// Arrange
			var input    = new[] { 1, 2, 3 };
			var expected = new[] { 1, 2, 3 };

			// Act
			ArrayHelpers.InitializeNullArray(ref input);

			// Assert
			Assert.That(input, Is.EqualTo(expected));
		}
	}
}
