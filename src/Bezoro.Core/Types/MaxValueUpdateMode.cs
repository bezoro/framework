namespace Bezoro.Core.Types;

/// <summary>
///     Defines how the current value should be adjusted when the max value changes.
/// </summary>
public enum MaxValueUpdateMode
{
	/// <summary>
	///     Clamp the current value to the new max.
	/// </summary>
	ClampCurrent,
	/// <summary>
	///     Preserve the current value as a percentage of the previous max.
	/// </summary>
	PreservePercentage
}
