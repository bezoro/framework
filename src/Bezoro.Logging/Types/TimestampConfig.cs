namespace Bezoro.Logging.Types;

/// <summary>
///     Configuration for timestamp metadata.
/// </summary>
public readonly struct TimestampConfig
{
	/// <summary>
	///     Creates an enabled timestamp configuration with custom format.
	/// </summary>
	/// <param name="format">Custom DateTime format string.</param>
	public static TimestampConfig Create(string format) => new() { Enabled = true, Format = format };

	/// <summary>
	///     Creates an enabled timestamp configuration with default format.
	/// </summary>
	public static TimestampConfig Default => new() { Enabled = true, Format = "HH:mm:ss.fff" };

	/// <summary>
	///     Disabled timestamp configuration.
	/// </summary>
	public static TimestampConfig Disabled => new() { Enabled = false, Format = string.Empty };

	/// <summary>
	///     Whether timestamp is enabled.
	/// </summary>
	public bool Enabled { get; init; }

	/// <summary>
	///     The timestamp format string (default: "HH:mm:ss.fff").
	/// </summary>
	public string Format { get; init; }
}
