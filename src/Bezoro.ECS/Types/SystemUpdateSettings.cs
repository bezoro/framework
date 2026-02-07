using Bezoro.Core.Utilities;

namespace Bezoro.ECS.Types;

/// <summary>
///     Defines how frequently a system should run.
/// </summary>
public readonly struct SystemUpdateSettings
{
	/// <summary>
	///     Initializes a new instance of the <see cref="SystemUpdateSettings" /> struct.
	/// </summary>
	/// <param name="intervalSeconds">The minimum interval, in seconds, between updates. Use 0 for every update.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="intervalSeconds" /> is negative.</exception>
	public SystemUpdateSettings(float intervalSeconds)
	{
		if (intervalSeconds < 0f)
			throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Interval must be non-negative.");

		IntervalSeconds = intervalSeconds;
	}

	/// <summary>
	///     Gets settings for systems that run every tick.
	/// </summary>
	public static SystemUpdateSettings EveryTick => new(0f);

	/// <summary>
	///     Gets the minimum interval, in seconds, between updates.
	/// </summary>
	public float IntervalSeconds { get; }

	/// <summary>
	///     Creates settings for a fixed update interval.
	/// </summary>
	/// <param name="intervalSeconds">The minimum interval, in seconds, between updates.</param>
	/// <returns>The configured update settings.</returns>
	public static SystemUpdateSettings FixedInterval(float intervalSeconds) => new(intervalSeconds);

	/// <summary>
	///     Creates settings for a fixed update interval.
	/// </summary>
	/// <param name="interval">The minimum interval between updates.</param>
	/// <returns>The configured update settings.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval" /> is negative.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval" /> is too large.</exception>
	public static SystemUpdateSettings FixedInterval(TimeSpan interval)
	{
		if (interval < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be non-negative.");

		double seconds = interval.TotalSeconds;
		if (seconds > float.MaxValue)
			throw new ArgumentOutOfRangeException(
				nameof(interval), "Interval must fit within single-precision seconds."
			);

		return new((float)seconds);
	}

	/// <summary>
	///     Creates settings for a fixed update interval.
	/// </summary>
	/// <param name="intervalMilliseconds">The minimum interval, in milliseconds, between updates.</param>
	/// <returns>The configured update settings.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="intervalMilliseconds" /> is negative.</exception>
	public static SystemUpdateSettings FixedInterval(int intervalMilliseconds)
	{
		if (intervalMilliseconds < 0)
			throw new ArgumentOutOfRangeException(nameof(intervalMilliseconds), "Interval must be non-negative.");

		return new(intervalMilliseconds * Constants.SECONDS_PER_MILLISECOND);
	}

	/// <summary>
	///     Creates settings for a fixed update interval.
	/// </summary>
	/// <param name="intervalMilliseconds">The minimum interval, in milliseconds, between updates.</param>
	/// <returns>The configured update settings.</returns>
	public static SystemUpdateSettings FixedInterval(uint intervalMilliseconds) =>
		new(intervalMilliseconds * Constants.SECONDS_PER_MILLISECOND);
}
