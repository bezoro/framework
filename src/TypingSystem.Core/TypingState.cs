using System;

namespace TypingSystem.Core
{
	public readonly struct TypingState
	{
		public readonly byte   CorrectCount;
		public readonly ushort MistakeCount;
		public readonly byte   Position;

		public TypingState(byte position, byte correctCount, ushort mistakeCount)
		{
			if (position > correctCount)
				throw new ArgumentOutOfRangeException(
					nameof(position),
					position,
					"Position cannot be greater than correct count.");

			if (correctCount > position + 1)
				throw new ArgumentOutOfRangeException(
					nameof(correctCount),
					correctCount,
					"Correct count cannot be greater than position + 1.");

			Position     = position;
			CorrectCount = correctCount;
			MistakeCount = mistakeCount;
		}

		public static TypingState Initial => new(0, 0, 0);

		public TypingState WithMistake()
		{
			if (MistakeCount >= ushort.MaxValue)
			{
				throw new InvalidOperationException("Mistake count cannot exceed 65535.");
			}

			return new TypingState(Position, CorrectCount, (ushort)(MistakeCount + 1));
		}

		public TypingState WithCorrect()
		{
			if (Position >= byte.MaxValue)
			{
				throw new InvalidOperationException("Position cannot exceed 255.");
			}

			if (CorrectCount >= byte.MaxValue)
			{
				throw new InvalidOperationException("Correct count cannot exceed 255.");
			}

			return new TypingState(
				(byte)(Position + 1),
				(byte)(CorrectCount + 1),
				MistakeCount);
		}

		public bool IsComplete(byte targetLength)
		{
			if (targetLength < Position)
				throw new ArgumentOutOfRangeException(
					nameof(targetLength),
					targetLength,
					"Target length cannot be less than position.");

			if (targetLength < CorrectCount)
				throw new ArgumentOutOfRangeException(
					nameof(targetLength),
					targetLength,
					"Target length cannot be less than correct count.");

			return Position == targetLength - 1 && CorrectCount == targetLength;
		}
	}
}
