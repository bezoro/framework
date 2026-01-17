namespace Bezoro.Logging.Types;

/// <summary>
///     Configuration for file location metadata.
/// </summary>
public readonly struct FileLocationConfig
{
	/// <summary>
	///     Disabled file location configuration.
	/// </summary>
	public static FileLocationConfig Disabled => new() { Enabled = false, ShowFullPath = false };

	/// <summary>
	///     Creates an enabled file location configuration (filename only).
	/// </summary>
	public static FileLocationConfig FilenameOnly => new() { Enabled = true, ShowFullPath = false };

	/// <summary>
	///     Creates an enabled file location configuration (full path).
	/// </summary>
	public static FileLocationConfig FullPath => new() { Enabled = true, ShowFullPath = true };

	/// <summary>
	///     Whether file location is enabled.
	/// </summary>
	public bool Enabled { get; init; }

	/// <summary>
	///     Whether to show full path or just filename.
	/// </summary>
	public bool ShowFullPath { get; init; }
}
