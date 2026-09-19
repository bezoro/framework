namespace Bezoro.TypingSystem.Types;

/// <summary>
///     Represents the result of a typing validation operation.
/// </summary>
public readonly struct TypingResult
{
	/// <summary>
	///     Initializes a new instance of the <see cref="TypingResult" /> struct.
	/// </summary>
	/// <param name="status">The validation status.</param>
	/// <param name="expected">The expected character at the validation position.</param>
	/// <param name="position">The position in the target sequence where validation occurred.</param>
	/// <param name="input">The input character that was validated.</param>
	/// <param name="isCorrect">A value indicating whether the input was correct.</param>
	/// <param name="isComplete">A value indicating whether the typing operation is complete.</param>
	/// <param name="nextPosition">The next position to validate in the target sequence.</param>
	/// <param name="targetLength">The total length of the target sequence.</param>
	[Obsolete("Use Match, Mismatch, Completed, EmptyTarget, or PositionOutOfRange instead.")]
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

	/// <summary>
	///     Creates a successful result that completes the target sequence.
	/// </summary>
	/// <param name="expected">The expected character at the validation position.</param>
	/// <param name="position">The position in the target sequence where validation occurred.</param>
	/// <param name="input">The input character that was validated.</param>
	/// <param name="targetLength">The total length of the target sequence.</param>
	/// <returns>A completed typing result.</returns>
	#pragma warning disable CS0618
	public static TypingResult Completed(char expected, byte position, char input, byte targetLength) =>
		new(
			TypingValidationStatus.Completed,
			expected,
			position,
			input,
			true,
			true,
			targetLength,
			targetLength
		);

	/// <summary>
	///     Creates a faulted result for an empty target sequence.
	/// </summary>
	/// <param name="position">The requested position in the target sequence.</param>
	/// <param name="input">The input character that was validated.</param>
	/// <returns>An empty-target typing result.</returns>
	public static TypingResult EmptyTarget(byte position, char input) =>
		new(
			TypingValidationStatus.EmptyTarget,
			default,
			position,
			input,
			false,
			false,
			0,
			0
		);

	/// <summary>
	///     Creates a successful result for an input that matches the expected character.
	/// </summary>
	/// <param name="expected">The expected character at the validation position.</param>
	/// <param name="position">The position in the target sequence where validation occurred.</param>
	/// <param name="input">The input character that was validated.</param>
	/// <param name="targetLength">The total length of the target sequence.</param>
	/// <returns>A matching typing result.</returns>
	public static TypingResult Match(char expected, byte position, char input, byte targetLength) =>
		new(
			TypingValidationStatus.Match,
			expected,
			position,
			input,
			true,
			false,
			(byte)(position + 1),
			targetLength
		);

	/// <summary>
	///     Creates an unsuccessful result for an input that does not match the expected character.
	/// </summary>
	/// <param name="expected">The expected character at the validation position.</param>
	/// <param name="position">The position in the target sequence where validation occurred.</param>
	/// <param name="input">The input character that was validated.</param>
	/// <param name="targetLength">The total length of the target sequence.</param>
	/// <returns>A mismatching typing result.</returns>
	public static TypingResult Mismatch(char expected, byte position, char input, byte targetLength) =>
		new(
			TypingValidationStatus.Mismatch,
			expected,
			position,
			input,
			false,
			false,
			position,
			targetLength
		);

	/// <summary>
	///     Creates a faulted result for a validation position outside the target sequence.
	/// </summary>
	/// <param name="position">The requested position in the target sequence.</param>
	/// <param name="targetLength">The total length of the target sequence.</param>
	/// <param name="input">The input character that was validated.</param>
	/// <returns>A position-out-of-range typing result with a next position clamped to the target sequence.</returns>
	public static TypingResult PositionOutOfRange(byte position, byte targetLength, char input)
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
			targetLength
		);
	}
	#pragma warning restore CS0618

	private static byte CalculateNextPositionForOutOfRange(byte position, byte targetLength)
	{
		if (targetLength == 0) return 0;

		var maxIndex = (byte)(targetLength - 1);
		return position > maxIndex ? maxIndex : position;
	}
}
