namespace Bezoro.TypingSystem.Types;

/// <summary>
///     Represents the current state of a typing operation for a single word.
/// </summary>
public readonly struct TypingState
{
	/// <summary>
	///     The number of correctly typed characters.
	/// </summary>
	public readonly byte CorrectCount;

	/// <summary>
	///     The current position in the target sequence.
	/// </summary>
	public readonly byte Position;

	/// <summary>
	///     The number of mistakes made during typing.
	/// </summary>
	public readonly ushort MistakeCount;

	/// <summary>
	///     Initializes a new instance of the <see cref="TypingState" /> struct.
	/// </summary>
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

	/// <summary>
	///     Gets the initial typing state.
	/// </summary>
	public static TypingState Initial => new(0, 0, 0);

	/// <summary>
	///     Checks if the typing operation is complete for the given target length.
	/// </summary>
	/// <param name="targetLength">The length of the target sequence.</param>
	/// <returns><see langword="true" /> if complete; otherwise, <see langword="false" />.</returns>
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

	/// <summary>
	///     Creates a new state reflecting a correct input.
	/// </summary>
	/// <returns>A new <see cref="TypingState" /> instance.</returns>
	public TypingState WithCorrect()
	{
		if (Position >= byte.MaxValue) throw new InvalidOperationException("Position cannot exceed 255.");

		if (CorrectCount >= byte.MaxValue) throw new InvalidOperationException("Correct count cannot exceed 255.");

		return new(
			(byte)(Position + 1),
			(byte)(CorrectCount + 1),
			MistakeCount);
	}

	/// <summary>
	///     Creates a new state reflecting a mistake input.
	/// </summary>
	/// <returns>A new <see cref="TypingState" /> instance.</returns>
	public TypingState WithMistake()
	{
		if (MistakeCount >= ushort.MaxValue) throw new InvalidOperationException("Mistake count cannot exceed 65535.");

		return new(Position, CorrectCount, (ushort)(MistakeCount + 1));
	}
}
