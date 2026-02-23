using Bezoro.TypingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.TypingSystem.Tests.Types;

[TestSubject(typeof(TypingResult))]
public class TypingResultCompletedTests
{
	[Fact]
	public void Completed_WhenCalled_ShouldReturnExpectedResult()
	{
		const byte TARGET_LENGTH = 5;
		const byte POSITION      = TARGET_LENGTH - 1;
		const char EXPECTED      = 'd';
		const char INPUT         = 'd';

		var result = TypingResult.Completed(EXPECTED, POSITION, INPUT, TARGET_LENGTH);

		result.Status.Should().Be(TypingValidationStatus.Completed);
		result.IsCorrect.Should().BeTrue();
		result.IsComplete.Should().BeTrue();
		result.IsFaulted.Should().BeFalse();
		result.Expected.Should().Be(EXPECTED);
		result.Input.Should().Be(INPUT);
		result.Position.Should().Be(POSITION);
		result.TargetLength.Should().Be(TARGET_LENGTH);
		result.NextPosition.Should().Be(TARGET_LENGTH);
	}
}
