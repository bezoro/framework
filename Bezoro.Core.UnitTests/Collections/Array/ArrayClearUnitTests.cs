using System.Linq;
using Bezoro.Core.Collections.Array;
using NUnit.Framework;

namespace Bezoro.Core.UnitTests.Collections.Array;

[TestFixture]
[TestOf(typeof(ArrayHelpers))]
public class ArrayClearUnitTests
{
#region Test Methods

	[Test]
	public void Clear_WhenArrayExceedsParallelThreshold_UsesParallelMethodToResetAllElements()
	{
		// Arrange
		var threshold = ArrayHelpers.ParallelThreshold;
		var array     = new int[threshold * 2];

		for (var i = 0 ; i < array.Length ; i++)
			array[i] = i + 1;

		// Act
		ArrayHelpers.Clear(ref array);

		// Assert
		Assert.That(array.All(element => element == default), Is.True);
	}

	[Test]
	public void Clear_WhenArrayIsBelowParallelThreshold_UsesSequentialMethodToResetAllElements()
	{
		// Arrange
		int[] array = { 1, 2, 3, 4, 5 };

		// Act
		ArrayHelpers.Clear(ref array);

		// Assert
		Assert.That(array.All(element => element == default), Is.True);
	}

	[Test]
	public void Clear_WhenArrayIsNull_DoesNotThrowException()
	{
		// Arrange
		int[] array = null;

		// Act & Assert
		Assert.DoesNotThrow(() => ArrayHelpers.Clear(ref array));
	}

	[Test]
	public void Clear_WhenArrayIsEmpty_DoesNotModifyArray()
	{
		// Arrange
		var array = System.Array.Empty<int>();

		// Act
		ArrayHelpers.Clear(ref array);

		// Assert
		Assert.That(array, Is.Empty);
	}

#endregion
}
