namespace Bezoro.Core.Types.Exceptions;

/// <summary>
///     Exception thrown when a value is smaller than the configured minimum.
/// </summary>
/// <remarks>
///     The exception message includes the offending value and the permitted minimum.
/// </remarks>
public class ValueTooSmallException : Exception
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ValueTooSmallException" /> class.
	/// </summary>
	/// <param name="value">The value that was too small.</param>
	/// <param name="min">The minimum permitted value.</param>
	public ValueTooSmallException(float value, float min)
		: base($"Value '{value}' is smaller than the minimum allowed value '{min}'.") { }
}
