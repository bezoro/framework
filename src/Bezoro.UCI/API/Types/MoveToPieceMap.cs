namespace Bezoro.UCI.API.Types;

public readonly record struct MoveToPieceMap(string Move, char Piece)
{
	public static MoveToPieceMap Map(Fen fen, string move)
	{
		if (string.IsNullOrWhiteSpace(move) || move.Length < 4)
			return new(move, '\0');

		string from = move[..2];

		var board = BoardState.FromFen(fen);
		if (board.TryGetPieceAt(from, out var piece) && piece.HasValue)
			return new(move, piece.Value.Char);

		return new(move, '\0');
	}
}
