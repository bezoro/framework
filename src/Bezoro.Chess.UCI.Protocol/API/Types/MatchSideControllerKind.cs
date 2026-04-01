namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Describes how a match side is controlled within a playable match session.
/// </summary>
public enum MatchSideControllerKind
{
	/// <summary>
	///     The side is driven externally by submitted move strings, such as a local human player or a remote client.
	/// </summary>
	Manual = 0,

	/// <summary>
	///     The side is driven automatically by the configured UCI playing engine.
	/// </summary>
	Engine = 1
}
