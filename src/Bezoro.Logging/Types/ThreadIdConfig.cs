namespace Bezoro.Logging.Types;

/// <summary>
///     Configuration for thread ID metadata.
/// </summary>
public readonly struct ThreadIdConfig
{
	/// <summary>
	///     Disabled thread ID configuration.
	/// </summary>
	public static ThreadIdConfig Off => new() { Enabled = false };

	/// <summary>
	///     Enabled thread ID configuration.
	/// </summary>
	public static ThreadIdConfig On => new() { Enabled = true };

	/// <summary>
	///     Whether thread ID is enabled.
	/// </summary>
	public bool Enabled { get; init; }
}
