using Bezoro.TypingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.TypingSystem.Tests;

[TestSubject(typeof(TypingResult))]
public class TypingResultPositionOutOfRangeTests
{
	[Fact]
	public void WhenPositionExceedsTargetLength_ShouldClampNextPosition()
	{
		const byte POSITION      = 5;
		const byte TARGET_LENGTH = 3;
		const char INPUT         = 'a';

		var result = TypingResult.PositionOutOfRange(POSITION, TARGET_LENGTH, INPUT);

		result.Status.Should().Be(TypingValidationStatus.PositionOutOfRange);
		result.IsFaulted.Should().BeTrue();
		result.IsCorrect.Should().BeFalse();
		result.IsComplete.Should().BeFalse();
		result.Position.Should().Be(POSITION);
		result.TargetLength.Should().Be(TARGET_LENGTH);
		result.NextPosition.Should().Be(TARGET_LENGTH - 1);
		result.Input.Should().Be(INPUT);
		result.Expected.Should().Be(default);
	}

	[Fact]
	public void WhenTargetLengthIsZero_ShouldReturnZeroForNextPosition()
	{
		const byte POSITION      = 10;
		const byte TARGET_LENGTH = 0;
		const char INPUT         = 'a';

		var result = TypingResult.PositionOutOfRange(POSITION, TARGET_LENGTH, INPUT);

		result.Status.Should().Be(TypingValidationStatus.PositionOutOfRange);
		result.NextPosition.Should().Be(0);
		result.TargetLength.Should().Be(TARGET_LENGTH);
		result.IsFaulted.Should().BeTrue();
	}
}
