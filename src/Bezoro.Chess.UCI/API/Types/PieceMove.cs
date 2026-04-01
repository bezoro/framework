namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Represents a concrete piece movement between two squares.
/// </summary>
/// <param name="Piece">The piece that moved.</param>
/// <param name="From">Origin square in algebraic notation.</param>
/// <param name="To">Destination square in algebraic notation.</param>
public readonly record struct PieceMove(
	Piece  Piece,
	string From,
	string To
);
