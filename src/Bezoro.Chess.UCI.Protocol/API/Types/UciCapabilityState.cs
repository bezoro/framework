namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents whether a specific UCI capability is supported by the current engine.
/// </summary>
public enum UciCapabilityState
{
	/// <summary>
	///     Support has not been determined yet.
	/// </summary>
	Unknown,

	/// <summary>
	///     The capability is supported by the current engine.
	/// </summary>
	Supported,

	/// <summary>
	///     The capability is not supported by the current engine.
	/// </summary>
	Unsupported
}
