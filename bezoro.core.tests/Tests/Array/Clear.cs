using System.Linq;
using Bezoro.Core.Collections.Array;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Array
{
	[TestFixture]
	[TestOf(typeof(ArrayHelpers))]
	public class Clear
	{
		[Test]
		public void WhenArrayLengthIsGreaterThanParallelThreshold_AllElementsAreSetToDefault()
		{
			// Arrange
			var threshold = ArrayHelpers.PARALLEL_THRESHOLD;
			var array     = new int[threshold * 2];

			for (var i = 0 ; i < array.Length ; i++)
				array[i] = i + 1;

			// Act
			ArrayHelpers.Clear(ref array);

			// Assert
			Assert.That(array.All(element => element == default), Is.True);
		}

		[Test]
		public void WhenArrayLengthIsLessThanParallelThreshold_AllElementsAreSetToDefault()
		{
			// Arrange
			int[] array = { 1, 2, 3, 4, 5 };

			// Act
			ArrayHelpers.Clear(ref array);

			// Assert
			Assert.That(array.All(element => element == default), Is.True);
		}
	}
}
