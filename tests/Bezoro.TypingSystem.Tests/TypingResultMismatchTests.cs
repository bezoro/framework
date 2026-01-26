using Bezoro.TypingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.TypingSystem.Tests;

[TestSubject(typeof(TypingResult))]
public class TypingResultMismatchTests
{
	[Fact]
	public void WhenCalled_ShouldReturnExpectedResult()
	{
		const char EXPECTED      = 'c';
		const byte POSITION      = 2;
		const char INPUT         = 'z';
		const byte TARGET_LENGTH = 4;

		var result = TypingResult.Mismatch(EXPECTED, POSITION, INPUT, TARGET_LENGTH);

		result.Status.Should().Be(TypingValidationStatus.Mismatch);
		result.IsCorrect.Should().BeFalse();
		result.IsComplete.Should().BeFalse();
		result.IsFaulted.Should().BeFalse();
		result.Expected.Should().Be(EXPECTED);
		result.Input.Should().Be(INPUT);
		result.Position.Should().Be(POSITION);
		result.TargetLength.Should().Be(TARGET_LENGTH);
		result.NextPosition.Should().Be(POSITION);
	}
}
