namespace Bezoro.UCI.API.Types;

/// <summary>
///     Identifies the connected UCI engine.
/// </summary>
public readonly record struct UciEngineInfo(string Name, string Author)
{
	/// <summary>
	///     Gets an empty metadata value used before handshake data is available.
	/// </summary>
	public static UciEngineInfo Empty { get; } = new(string.Empty, string.Empty);
}
