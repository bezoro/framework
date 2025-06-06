using Bezoro.Core.Collections.Array;
using Bezoro.Core.Logging;
using Moq;
using NUnit.Framework;

namespace Bezoro.Core.UnitTests.Collections.Array;

[TestFixture]
[TestOf(typeof(ArrayHelpers))]
public class ArrayResizeByFactorUnitTests
{
	private Mock<ILogger> _mockLogger;

#region Setup/Teardown Methods

	[SetUp]
	public void SetUp() =>
		_mockLogger = new();

#endregion

#region Test Methods

	[Test]
	public void ResizeByFactor_WhenArrayIsEmpty_ThenArraySizeRemainsZeroRegardlessOfFactor()
	{
		// Arrange
		var       array  = System.Array.Empty<int>();
		const int factor = 3;

		// Act
		ArrayHelpers.ResizeByFactor(ref array, factor);

		// Assert
		Assert.That(array.Length, Is.EqualTo(0), "Empty array multiplied should still stay empty.");
	}

	[Test]
	public void ResizeByFactor_WhenFactorIsOneOrLess_ThenArraySizeRemainsUnchanged([Range(1, -5)] int factor)
	{
		// Arrange
		int[] array = { 1, 2, 3 };

		// Act
		ArrayHelpers.ResizeByFactor(ref array, factor);

		// Assert
		Assert.That(array.Length, Is.EqualTo(3), "Array size should remain unchanged.");
	}

	[Test]
	public void ResizeByFactor_WhenFactorIsPositive_ThenArraySizeIncreasesByFactor()
	{
		// Arrange
		int[] array  = { 1, 2, 3 };
		var   factor = 2;

		// Act
		ArrayHelpers.ResizeByFactor(ref array, factor);

		// Assert
		Assert.That(array.Length, Is.EqualTo(6), "Array size should be multiplied by the factor.");
	}

#endregion
}
