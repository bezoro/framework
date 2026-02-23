using Bezoro.TypingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.TypingSystem.Tests.Types;

[TestSubject(typeof(TypingResult))]
public class TypingResultEmptyTargetTests
{
	[Fact]
	public void EmptyTarget_WhenCalled_ShouldReturnExpectedResult()
	{
		const byte POSITION = 5;
		const char INPUT    = 'x';

		var result = TypingResult.EmptyTarget(POSITION, INPUT);

		result.Status.Should().Be(TypingValidationStatus.EmptyTarget);
		result.IsFaulted.Should().BeTrue();
		result.IsCorrect.Should().BeFalse();
		result.IsComplete.Should().BeFalse();
		result.Position.Should().Be(POSITION);
		result.TargetLength.Should().Be(0);
		result.NextPosition.Should().Be(0);
		result.Input.Should().Be(INPUT);
		result.Expected.Should().Be(default);
	}
}
