using Bezoro.Core.Collections.Array;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Array
{
	[TestFixture]
	[TestOf(typeof(ArrayHelpers))]
	public class InitializeNullArray
	{
		[TestCase(null, new int[0], TestName = "InitializeNullArray_NullInput_InitializesToEmpty")]
		[TestCase(
			new[] { 1, 2, 3 }, new[] { 1, 2, 3 }, TestName = "InitializeNullArray_NonNullInput_PreservesOriginal")]
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
