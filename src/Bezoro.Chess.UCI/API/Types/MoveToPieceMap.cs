namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Associates coordinate move notation with the piece occupying its source square.
/// </summary>
/// <param name="Move">The original move notation.</param>
/// <param name="Piece">The resolved piece character, or the null character when unresolved.</param>
public readonly record struct MoveToPieceMap(string Move, char Piece)
{
	/// <summary>
	///     Resolves the moving piece for coordinate notation against a FEN position.
	/// </summary>
	/// <param name="fen">The board position.</param>
	/// <param name="move">The coordinate move notation.</param>
	/// <returns>A mapping containing the resolved piece character, or the null character when resolution fails.</returns>
	public static MoveToPieceMap Map(Fen fen, string move)
	{
		if (string.IsNullOrWhiteSpace(move) || move.Length < 4)
			return new(move, '\0');

		string from = move[..2];

		var board = BoardState.FromFen(fen);
		if (board.HasValue && board.Value.TryGetPieceAt(from, out var piece) && piece.HasValue)
			return new(move, piece.Value.Char);

		return new(move, '\0');
	}
}
