using System;
using FluentAssertions;
using JetBrains.Annotations;

namespace TypingSystem.Core.Tests;

[TestSubject(typeof(TypingValidator))]
public static class TypingValidatorTests
{
	public static class UnitTests
	{
		public class ValidateInputTests
		{
			[Fact]
			public void WhenInputCompletesTarget_ShouldReturnCompletedStatus()
			{
				var  target   = "abc".AsSpan();
				var  position = (byte)(target.Length - 1);
				char input    = target[position];

				var result = TypingValidator.ValidateInput(target, position, input);

				result.Status.Should().Be(TypingValidationStatus.Completed);
				result.IsCorrect.Should().BeTrue();
				result.IsComplete.Should().BeTrue();
				result.IsFaulted.Should().BeFalse();
				result.NextPosition.Should().Be(target.Length);
			}

			[Fact]
			public void WhenInputDoesNotMatch_ShouldReturnMismatchStatus()
			{
				var        target   = "abc".AsSpan();
				const int  position = 0;
				const char input    = 'z';

				var result = TypingValidator.ValidateInput(target, position, input);

				result.Status.Should().Be(TypingValidationStatus.Mismatch);
				result.IsCorrect.Should().BeFalse();
				result.IsComplete.Should().BeFalse();
				result.IsFaulted.Should().BeFalse();
				result.Expected.Should().Be(target[position]);
				result.NextPosition.Should().Be(position);
			}

			[Fact]
			public void WhenInputMatches_ShouldReturnMatchStatus()
			{
				var        target   = "abc".AsSpan();
				const int  position = 1;
				const char input    = 'b';

				var result = TypingValidator.ValidateInput(target, position, input);

				result.Status.Should().Be(TypingValidationStatus.Match);
				result.IsCorrect.Should().BeTrue();
				result.IsComplete.Should().BeFalse();
				result.IsFaulted.Should().BeFalse();
				result.NextPosition.Should().Be(position + 1);
				result.Expected.Should().Be(target[position]);
				result.TargetLength.Should().Be(target.Length);
			}

			[Theory]
			[InlineData(3,   2)]
			[InlineData(255, 2)]
			public void WhenPositionIsOutOfRange_ShouldReturnFaultedStatus(byte position, byte expectedNextPosition)
			{
				var        target = "abc".AsSpan();
				const char input  = 'a';

				var result = TypingValidator.ValidateInput(target, position, input);

				result.Status.Should().Be(TypingValidationStatus.PositionOutOfRange);
				result.IsFaulted.Should().BeTrue();
				result.IsCorrect.Should().BeFalse();
				result.IsComplete.Should().BeFalse();
				result.NextPosition.Should().Be(expectedNextPosition);
				result.TargetLength.Should().Be(target.Length);
			}

			[Fact]
			public void WhenTargetIsEmpty_ShouldReturnEmptyTargetStatus()
			{
				var        target = ReadOnlySpan<char>.Empty;
				const char input  = 'x';

				var result = TypingValidator.ValidateInput(target, 0, input);

				result.Status.Should().Be(TypingValidationStatus.EmptyTarget);
				result.IsFaulted.Should().BeTrue();
				result.IsCorrect.Should().BeFalse();
				result.IsComplete.Should().BeFalse();
				result.NextPosition.Should().Be(0);
				result.TargetLength.Should().Be(0);
			}

			[Fact]
			public void WhenTargetLengthEqualsMaximum_ShouldValidateSuccessfully()
			{
				string word = new('a', byte.MaxValue);
				var    target = word.AsSpan();
				const byte POSITION = 0;
				const char INPUT    = 'a';

				var result = TypingValidator.ValidateInput(target, POSITION, INPUT);

				result.Status.Should().Be(TypingValidationStatus.Match);
				result.TargetLength.Should().Be(byte.MaxValue);
				result.NextPosition.Should().Be(POSITION + 1);
			}

			[Fact]
			public void WhenTargetExceedsMaximumLength_ShouldThrow()
			{
				string word = new('a', byte.MaxValue + 1);

				Action action = () => TypingValidator.ValidateInput(word.AsSpan(), 0, 'a');

				action.Should()
					.Throw<ArgumentOutOfRangeException>()
					.WithParameterName("target")
					.WithMessage("*Target length cannot exceed 255 characters.*");
			}
		}
	}
}
