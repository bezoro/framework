namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a secondary board move caused by a primary move, such as rook motion during castling.
/// </summary>
/// <param name="From">Source square.</param>
/// <param name="To">Destination square.</param>
/// <param name="Piece">Moved piece.</param>
public readonly record struct PlayableMatchSecondaryMove(
	string             From,
	string             To,
	PlayableMatchPiece Piece
);
