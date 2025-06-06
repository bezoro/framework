using Bezoro.Core.Collections.Array;
using Bezoro.Core.Logging;
using Moq;
using NUnit.Framework;

namespace Bezoro.Core.UnitTests.Collections.Array;

[TestFixture]
[TestOf(typeof(ArrayHelpers))]
public class ResizeByFactor
{
	private Mock<ILogger> _mockLogger;

#region Setup/Teardown Methods

	[SetUp]
	public void SetUp() =>
		_mockLogger = new();

#endregion

#region Test Methods

	[Test]
	public void WhenArrayIsEmpty_ArraySizeRemainsZeroRegardlessOfFactor()
	{
		// Arrange
		var       array  = System.Array.Empty<int>();
		const int factor = 3;

		// Act
		ArrayHelpers.ResizeByFactor(ref array, factor);

		// Assert
		Assert.That(array.Length, Is.EqualTo(0), "Empty array multiplied should still stay empty.");

		// _mockLogger.Verify(
		// logger => logger.Log(It.Is<string>(msg => !string.IsNullOrEmpty(msg))),
		// Times.Once
		// );
	}

	[Test]
	public void WhenFactorIsOneOrLess_ArraySizeRemainsUnchanged([Range(1, -5)] int factor)
	{
		// Arrange
		int[] array = { 1, 2, 3 };

		// Act
		ArrayHelpers.ResizeByFactor(ref array, factor);

		// Assert
		Assert.That(array.Length, Is.EqualTo(3), "Array size should remain unchanged.");

		// _mockLogger.Verify(
		// logger => logger.Log_Warning(It.Is<string>(msg => !string.IsNullOrEmpty(msg))),
		// Times.Once
		// );
	}

	[Test]
	public void WhenFactorIsPositive_ArraySizeIncreasesByFactor()
	{
		// Arrange
		int[] array  = { 1, 2, 3 };
		var   factor = 2;

		// Act
		ArrayHelpers.ResizeByFactor(ref array, factor);

		// Assert
		Assert.That(array.Length, Is.EqualTo(6), "Array size should be multiplied by the factor.");

		// _mockLogger.Verify(
		// logger => logger.Log(
		// It.Is<string>(msg => !string.IsNullOrEmpty(msg))),
		// Times.Once
		// );
	}

#endregion
}
