namespace Bezoro.Logging.Types;

/// <summary>
///     Configuration for frame count metadata.
/// </summary>
public readonly struct FrameCountConfig
{
	/// <summary>
	///     Creates an enabled frame count configuration with the specified provider.
	/// </summary>
	/// <param name="provider">Function that returns the current frame count (e.g., () => Time.frameCount in Unity).</param>
	public static FrameCountConfig Create(Func<int> provider) => new() { Enabled = true, Provider = provider };

	/// <summary>
	///     Disabled frame count configuration.
	/// </summary>
	public static FrameCountConfig Disabled => new() { Enabled = false, Provider = null };

	/// <summary>
	///     Whether frame count is enabled.
	/// </summary>
	public bool Enabled { get; init; }

	/// <summary>
	///     Provider function that returns the current frame count.
	/// </summary>
	public Func<int>? Provider { get; init; }
}
