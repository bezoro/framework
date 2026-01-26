namespace Bezoro.TypingSystem.Types;

/// <summary>
///     Represents the status of a typing validation.
/// </summary>
public enum TypingValidationStatus : byte
{
	/// <summary>
	///     The status is undefined.
	/// </summary>
	Undefined = 0,

	/// <summary>
	///     The input character matches the expected character.
	/// </summary>
	Match = 1,

	/// <summary>
	///     The input character matches the last character of the target sequence.
	/// </summary>
	Completed = 2,

	/// <summary>
	///     The input character does not match the expected character.
	/// </summary>
	Mismatch = 3,

	/// <summary>
	///     The target sequence is empty.
	/// </summary>
	EmptyTarget = 4,

	/// <summary>
	///     The validation position is out of range for the target sequence.
	/// </summary>
	PositionOutOfRange = 5
}

/// <summary>
///     Represents the result of a typing validation operation.
/// </summary>
public readonly struct TypingResult
{
	/// <summary>
	///     Initializes a new instance of the <see cref="TypingResult" /> struct.
	/// </summary>
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

	/// <summary>
	///     Gets a value indicating whether the typing operation is complete.
	/// </summary>
	public bool IsComplete { get; }

	/// <summary>
	///     Gets a value indicating whether the input was correct.
	/// </summary>
	public bool IsCorrect { get; }

	/// <summary>
	///     Gets a value indicating whether the validation failed due to an error (e.g., out of range).
	/// </summary>
	public bool IsFaulted => Status == TypingValidationStatus.EmptyTarget ||
							 Status == TypingValidationStatus.PositionOutOfRange;

	/// <summary>
	///     Gets the expected character at the validation position.
	/// </summary>
	public char Expected { get; }

	/// <summary>
	///     Gets the input character that was validated.
	/// </summary>
	public char Input { get; }

	/// <summary>
	///     Gets the next position to validate in the target sequence.
	/// </summary>
	public int NextPosition { get; }

	/// <summary>
	///     Gets the position in the target sequence where the validation occurred.
	/// </summary>
	public int Position { get; }

	/// <summary>
	///     Gets the total length of the target sequence.
	/// </summary>
	public int TargetLength { get; }

	/// <summary>
	///     Gets the status of the validation.
	/// </summary>
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
		if (targetLength == 0) return 0;

		var maxIndex = (byte)(targetLength - 1);
		return position > maxIndex ? maxIndex : position;
	}
}
