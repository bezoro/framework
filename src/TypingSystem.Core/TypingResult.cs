namespace TypingSystem.Core
{
	public enum TypingValidationStatus : byte
	{
		Undefined          = 0,
		Match              = 1,
		Completed          = 2,
		Mismatch           = 3,
		EmptyTarget        = 4,
		PositionOutOfRange = 5
	}

	public readonly struct TypingResult
	{
		public TypingResult(
			TypingValidationStatus status,
			char                   expected,
			byte                   position,
			char                   input,
			bool                   isCorrect,
			bool                   isComplete,
			byte                   nextPosition,
			byte                   targetLength)
		{
			Status       = status;
			Expected     = expected;
			Position     = position;
			Input        = input;
			IsCorrect    = isCorrect;
			IsComplete   = isComplete;
			NextPosition = nextPosition;
			TargetLength = targetLength;
		}

		public bool IsComplete { get; }

		public bool IsCorrect { get; }

		public bool IsFaulted => Status == TypingValidationStatus.EmptyTarget ||
								 Status == TypingValidationStatus.PositionOutOfRange;

		public char Expected { get; }

		public char Input { get; }

		public int NextPosition { get; }

		public int Position { get; }

		public int TargetLength { get; }

		public TypingValidationStatus Status { get; }

		internal static TypingResult Completed(char expected, byte position, char input, byte targetLength) =>
			new(
				TypingValidationStatus.Completed,
				expected,
				position,
				input,
				true,
				true,
				targetLength,
				targetLength);

		internal static TypingResult EmptyTarget(byte position, char input) =>
			new(
				TypingValidationStatus.EmptyTarget,
				default,
				position,
				input,
				false,
				false,
				0,
				0);

		internal static TypingResult Match(char expected, byte position, char input, byte targetLength) =>
			new(
				TypingValidationStatus.Match,
				expected,
				position,
				input,
				true,
				false,
				(byte)(position + 1),
				targetLength);

		internal static TypingResult Mismatch(char expected, byte position, char input, byte targetLength) =>
			new(
				TypingValidationStatus.Mismatch,
				expected,
				position,
				input,
				false,
				false,
				position,
				targetLength);

		internal static TypingResult PositionOutOfRange(byte position, byte targetLength, char input)
		{
			byte nextPosition = CalculateNextPositionForOutOfRange(position, targetLength);

			return new(
				TypingValidationStatus.PositionOutOfRange,
				default,
				position,
				input,
				false,
				false,
				nextPosition,
				targetLength);
		}

		private static byte CalculateNextPositionForOutOfRange(byte position, byte targetLength)
		{
			if (targetLength <= 0) return 0;

			var maxIndex = (byte)(targetLength - 1);
			return position > maxIndex ? maxIndex : position;
		}
	}
}
