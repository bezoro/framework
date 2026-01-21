namespace Bezoro.Logging.Types;

/// <summary>
///     Configuration for sequence number metadata.
/// </summary>
public readonly struct SequenceNumberConfig
{
	/// <summary>
	///     Disabled sequence number configuration.
	/// </summary>
	public static SequenceNumberConfig Off => new() { Enabled = false };

	/// <summary>
	///     Enabled sequence number configuration.
	/// </summary>
	public static SequenceNumberConfig On => new() { Enabled = true };

	/// <summary>
	///     Whether sequence number is enabled.
	/// </summary>
	public bool Enabled { get; init; }
}
