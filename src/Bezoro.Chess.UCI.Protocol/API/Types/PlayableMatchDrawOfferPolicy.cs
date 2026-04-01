namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Controls how pending draw offers behave after subsequent moves.
/// </summary>
public enum PlayableMatchDrawOfferPolicy
{
	/// <summary>
	///     Clears any pending offer as soon as the next move is completed.
	/// </summary>
	ExpireOnMove = 0,

	/// <summary>
	///     Keeps the pending offer available until it is explicitly accepted or declined.
	/// </summary>
	PersistUntilResponse
}
