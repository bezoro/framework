using Bezoro.Core.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Extensions;

[TestSubject(typeof(EnumExtensions))]
public static class EnumExtensionsTests
{
	public class Unit
	{
		[Fact]
		public void IsDefined_ShouldReturnFalse_WhenValueIsNotDefined()
		{
			// Arrange
			var value = (TestEnum)999;

			// Act
			bool result = value.IsDefined();

			// Assert
			result.Should().BeFalse();
		}

		[Fact]
		public void IsDefined_ShouldReturnTrue_WhenValueIsDefined()
		{
			// Arrange
			var value = TestEnum.First;

			// Act
			bool result = value.IsDefined();

			// Assert
			result.Should().BeTrue();
		}
	}

	private enum TestEnum
	{
		First,
		Second
	}
}


