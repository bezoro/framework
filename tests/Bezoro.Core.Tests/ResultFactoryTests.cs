using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;
using Xunit;

namespace Bezoro.Core.Tests;

[TestSubject(typeof(ResultFactory))]
public class ResultFactoryTests
{
	[Fact]
	public void Failed_ShouldCreateFailedResult()
	{
		// Arrange
		var reason = Substitute.For<IFailureReason>();

		// Act
		var result = ResultFactory.Failed<int>(reason);

		// Assert
		result.Success.Should().BeFalse();
		result.Failure.Should().Be(reason);
		result.TryGet(out _).Should().BeFalse();
	}

	[Fact]
	public void Succeeded_ShouldCreateSuccessfulResult()
	{
		// Arrange
		const int data = 42;

		// Act
		var result = ResultFactory.Succeeded(data);

		// Assert
		result.Success.Should().BeTrue();
		result.TryGet(out int value).Should().BeTrue();
		value.Should().Be(data);
	}
}
