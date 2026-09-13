namespace Bezoro.TypingSystem.Types;

/// <summary>
///     Represents the status of a typing validation.
/// </summary>
public enum TypingValidationStatus : byte
{
	/// <summary>The status is undefined.</summary>
	Undefined = 0,

	/// <summary>The input character matches the expected character.</summary>
	Match = 1,

	/// <summary>The input character matches the last character of the target sequence.</summary>
	Completed = 2,

	/// <summary>The input character does not match the expected character.</summary>
	Mismatch = 3,

	/// <summary>The target sequence is empty.</summary>
	EmptyTarget = 4,

	/// <summary>The validation position is out of range for the target sequence.</summary>
	PositionOutOfRange = 5
}
