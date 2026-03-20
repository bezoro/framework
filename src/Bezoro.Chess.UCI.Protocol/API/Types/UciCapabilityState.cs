namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents whether a specific UCI capability is supported by the current engine.
/// </summary>
public enum UciCapabilityState
{
	Unknown,
	Supported,
	Unsupported
}
