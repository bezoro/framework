using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace Bezoro.Core.Common.Primitives;

/// <summary>
///     Represents a 2D vector with unsigned integer components.
/// </summary>
[DebuggerDisplay("({X}, {Y})")]
public readonly record struct UIntVector2(uint X, uint Y) : IFormattable
{
	private const float WHOLE_TOLERANCE = 1e-6f;

	/// <summary>
	///     Explicitly converts a <see cref="Vector2" /> to a <see cref="UIntVector2" />.
	///     Conversion uses the same validation as <see cref="FromVector2(Vector2)" />.
	/// </summary>
	/// <param name="v">The source <see cref="Vector2" />.</param>
	/// <returns>
	///     The corresponding <see cref="UIntVector2" /> with X and Y rounded from <paramref name="v" />, if valid.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown if <paramref name="v" /> cannot be represented as <see cref="UIntVector2" />.
	/// </exception>
	public static explicit operator UIntVector2(Vector2 v) => FromVector2(v);

	/// <summary>
	///     Implicitly converts a <see cref="UIntVector2" /> to a <see cref="Vector2" />.
	/// </summary>
	/// <param name="v">The <see cref="UIntVector2" /> to convert.</param>
	/// <returns>
	///     A new <see cref="Vector2" /> with the same X and Y values as <paramref name="v" />.
	/// </returns>
	public static implicit operator Vector2(UIntVector2 v) => v.ToVector2();

	/// <summary>
	///     Attempts to convert a <see cref="Vector2" /> to a <see cref="UIntVector2" />.
	///     Returns true if successful, false otherwise.
	/// </summary>
	/// <param name="v">The <see cref="Vector2" /> to convert from.</param>
	/// <param name="result">
	///     When this method returns, contains the <see cref="UIntVector2" /> converted value if successful; otherwise,
	///     <c>null</c>.
	/// </param>
	/// <returns>
	///     <c>true</c> if the conversion was successful; <c>false</c> otherwise.
	/// </returns>
	public static bool TryFromVector2(Vector2 v, out UIntVector2? result)
	{
		if (IsValidComponent(v.X) && IsValidComponent(v.Y))
		{
			result = new((uint)MathF.Round(v.X), (uint)MathF.Round(v.Y));
			return true;
		}

		result = null;
		return false;
	}

	/// <summary>
	///     Creates a <see cref="UIntVector2" /> from a <see cref="Vector2" /> with validation.
	/// </summary>
	/// <param name="v">
	///     The <see cref="Vector2" /> whose components will be converted to unsigned integers.
	/// </param>
	/// <returns>
	///     A <see cref="UIntVector2" /> whose X and Y are the rounded values of <paramref name="v" />.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown if any component of <paramref name="v" /> is not finite, is negative,
	///     is not a whole number, or exceeds <see cref="UInt32.MaxValue" />.
	/// </exception>
	public static UIntVector2 FromVector2(Vector2 v)
	{
		if (!IsValidComponent(v.X) || !IsValidComponent(v.Y))
			throw new ArgumentOutOfRangeException(
				nameof(v),
				"Vector2 must contain finite, non-negative whole values within the range of UInt32.");

		return new((uint)MathF.Round(v.X), (uint)MathF.Round(v.Y));
	}

	/// <summary>
	///     Returns a string that represents the current vector in the format "(X, Y)".
	/// </summary>
	/// <returns>
	///     A string in the format "(X, Y)".
	/// </returns>
	public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

	/// <summary>
	///     Formats the value of the current instance using the specified format and culture-specific formatting information.
	/// </summary>
	/// <param name="format">
	///     The format to use. Supported formats are:
	///     <list type="bullet">
	///         <item>
	///             <description>"N" - Named format: "X=value, Y=value"</description>
	///         </item>
	///         <item>
	///             <description>"X" - CSV format: "value,value"</description>
	///         </item>
	///         <item>
	///             <description>null or any other value - Default format: "(value, value)"</description>
	///         </item>
	///     </list>
	/// </param>
	/// <param name="formatProvider">
	///     The provider to use to format the value, or <c>null</c> to use <see cref="CultureInfo.InvariantCulture" />.
	/// </param>
	/// <returns>
	///     A string representation of the current instance formatted according to the specified format and provider.
	/// </returns>
	public string ToString(string? format, IFormatProvider? formatProvider)
	{
		formatProvider ??= CultureInfo.InvariantCulture;
		return format?.ToUpperInvariant() switch
		{
			"N" => $"X={X.ToString(formatProvider)}, Y={Y.ToString(formatProvider)}",
			"X" => $"{X.ToString(formatProvider)},{Y.ToString(formatProvider)}",
			_   => $"({X.ToString(formatProvider)}, {Y.ToString(formatProvider)})"
		};
	}

	/// <summary>
	///     Converts this <see cref="UIntVector2" /> to a <see cref="Vector2" />.
	/// </summary>
	/// <returns>
	///     A <see cref="Vector2" /> where X and Y correspond to this instance's X and Y.
	/// </returns>
	public Vector2 ToVector2() => new(X, Y);

	/// <summary>
	///     Determines whether a float value can be used as a valid component
	///     for an unsigned integer vector (must be finite, a whole number within tolerance,
	///     not negative, and not greater than <see cref="UInt32.MaxValue" />).
	/// </summary>
	/// <param name="f">The float to check.</param>
	/// <returns>
	///     <c>true</c> if <paramref name="f" /> can be safely converted to uint; otherwise, <c>false</c>.
	/// </returns>
	private static bool IsValidComponent(float f)
	{
		if (float.IsNaN(f) || float.IsInfinity(f)) return false;
		if (f < 0f || f > uint.MaxValue) return false;

		return MathF.Abs(f - MathF.Round(f)) <= WHOLE_TOLERANCE;
	}
}
