namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Controls how engine-controlled turns behave when the engine search does not yield a usable best move.
/// </summary>
public enum PlayableMatchControlledMoveFallbackPolicy
{
	/// <summary>
	///     Falls back to a locally selected legal move using protocol-owned move classification.
	/// </summary>
	UseLocalFallback = 0,

	/// <summary>
	///     Propagates the engine search failure to the caller.
	/// </summary>
	Throw
}
