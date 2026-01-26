using Bezoro.TypingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.TypingSystem.Tests;

[TestSubject(typeof(TypingResult))]
public class TypingResultMatchTests
{
	[Fact]
	public void WhenCalled_ShouldReturnExpectedResult()
	{
		const char EXPECTED      = 'b';
		const byte POSITION      = 1;
		const char INPUT         = 'b';
		const byte TARGET_LENGTH = 4;

		var result = TypingResult.Match(EXPECTED, POSITION, INPUT, TARGET_LENGTH);

		result.Status.Should().Be(TypingValidationStatus.Match);
		result.IsCorrect.Should().BeTrue();
		result.IsComplete.Should().BeFalse();
		result.IsFaulted.Should().BeFalse();
		result.Expected.Should().Be(EXPECTED);
		result.Input.Should().Be(INPUT);
		result.Position.Should().Be(POSITION);
		result.TargetLength.Should().Be(TARGET_LENGTH);
		result.NextPosition.Should().Be(POSITION + 1);
	}

	[Fact]
	public void WhenTargetLengthIsMaximum_ShouldStillAdvance()
	{
		const byte TARGET_LENGTH = byte.MaxValue;
		const byte POSITION      = 0;
		const char EXPECTED      = 'a';
		const char INPUT         = 'a';

		var result = TypingResult.Match(EXPECTED, POSITION, INPUT, TARGET_LENGTH);

		result.Status.Should().Be(TypingValidationStatus.Match);
		result.TargetLength.Should().Be(TARGET_LENGTH);
		result.NextPosition.Should().Be(POSITION + 1);
	}
}
