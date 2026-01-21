using System.Runtime.CompilerServices;

namespace Bezoro.Core.Extensions;

/// <summary>
///     Provides extension methods for <see cref="byte" /> values.
/// </summary>
public static class ByteExtensions
{
	/// <summary>
	///     Determines whether <paramref name="value" /> is within the inclusive range defined by <paramref name="min" /> and
	///     <paramref name="max" />.
	/// </summary>
	/// <param name="value">The value to check.</param>
	/// <param name="min">The minimum (inclusive) value.</param>
	/// <param name="max">The maximum (inclusive) value.</param>
	/// <returns>
	///     <c>true</c> if <paramref name="value" /> is greater than or equal to <paramref name="min" /> and less than or
	///     equal to <paramref name="max" />; otherwise, <c>false</c>.
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsBetween(this byte value, byte min, byte max) => value >= min && value <= max;
}
