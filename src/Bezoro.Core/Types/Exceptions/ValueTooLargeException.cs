namespace Bezoro.Core.Types.Exceptions;

/// <summary>
///     Exception thrown when a value exceeds the configured maximum.
/// </summary>
/// <remarks>
///     The exception message includes the offending value and the permitted maximum.
/// </remarks>
public class ValueTooLargeException : Exception
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ValueTooLargeException" /> class.
	/// </summary>
	/// <param name="value">The value that exceeded the maximum limit.</param>
	/// <param name="max">The maximum allowed value.</param>
	public ValueTooLargeException(float value, float max)
		: base($"Value '{value}' is greater than the maximum allowed value '{max}'.") { }
}
