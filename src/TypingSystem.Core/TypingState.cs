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
