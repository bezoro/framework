namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Describes the current adjudicated terminal result for a match.
/// </summary>
public enum PlayableMatchResultReason
{
	/// <summary>No terminal result is currently present.</summary>
	None = 0,

	/// <summary>The side to move is checkmated.</summary>
	Checkmate,

	/// <summary>The side to move has no legal moves and is not in check.</summary>
	Stalemate,

	/// <summary>The same position occurred three times.</summary>
	ThreefoldRepetition,

	/// <summary>The fifty-move rule was reached.</summary>
	FiftyMoveRule,

	/// <summary>Neither side has sufficient mating material.</summary>
	InsufficientMaterial,

	/// <summary>A side resigned.</summary>
	Resignation,

	/// <summary>Both sides agreed to a draw.</summary>
	DrawAgreement,

	/// <summary>A side lost on time.</summary>
	Timeout
}
