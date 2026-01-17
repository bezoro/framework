namespace Bezoro.Core.Helpers;

/// <summary>
///     Provides helper extension methods for common floating-point range checks and mappings.
/// </summary>
public static class FloatHelper
{
	/// <summary>
	///     Determines whether the <paramref name="value" /> is within the inclusive range <paramref name="min" /> to
	///     <paramref name="max" />.
	/// </summary>
	/// <param name="value">The float to check.</param>
	/// <param name="min">The minimum inclusive bound.</param>
	/// <param name="max">The maximum inclusive bound.</param>
	/// <returns><c>true</c> if the value is between min and max (inclusive); <c>false</c> otherwise.</returns>
	public static bool IsWithinRange(this float value, float min, float max) =>
		value >= min && value <= max;

	/// <summary>
	///     Maps a value from one range to another, preserving proportional distance.
	/// </summary>
	/// <param name="value">The float value to map.</param>
	/// <param name="fromMin">The minimum bound of the input range.</param>
	/// <param name="fromMax">The maximum bound of the input range.</param>
	/// <param name="toMin">The minimum bound of the target range.</param>
	/// <param name="toMax">The maximum bound of the target range.</param>
	/// <returns>
	///     The corresponding value in [<paramref name="toMin" />, <paramref name="toMax" />] that maps linearly from
	///     <paramref name="value" /> in [<paramref name="fromMin" />, <paramref name="fromMax" />].
	/// </returns>
	public static float Map(this float value, float fromMin, float fromMax, float toMin, float toMax) =>
		(value - fromMin) / (fromMax - fromMin) * (toMax - toMin) + toMin;
}
