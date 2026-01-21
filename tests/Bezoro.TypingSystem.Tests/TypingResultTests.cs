using FluentAssertions;
using JetBrains.Annotations;
using Bezoro.TypingSystem.Types;

namespace Bezoro.TypingSystem.Tests;

[TestSubject(typeof(TypingResult))]
public static class TypingResultTests
{
	public static class UnitTests
	{
		public class CompletedTests
		{
			[Fact]
			public void WhenCalled_ShouldReturnExpectedResult()
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

		public class EmptyTargetTests
		{
			[Fact]
			public void WhenCalled_ShouldReturnExpectedResult()
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

		public class MatchTests
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

		public class MismatchTests
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

		public class PositionOutOfRangeTests
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
	}
}
