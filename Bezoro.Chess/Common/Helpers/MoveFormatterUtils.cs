using System.Text;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Moves.Models;

public static class MoveFormatter
{
	public static string ToText(this Move move, NotationStyle style) =>
		style switch
		{
			NotationStyle.Coordinate =>
				$"{move.From}{move.To}",

			NotationStyle.Uci =>
				$"{move.From}{move.To}{(move.IsPromotion ? move.PromoteTo.ToChar() : "")}",

			NotationStyle.LAN =>
				$"{move.PieceType.ToFenChar(move.MovingSide)}" +
				$"{move.From}"                                 +
				$"{(move.IsCapture ? "x" : "-")}"              +
				$"{move.To}"                                   +
				$"{(move.IsPromotion ? move.PromoteTo.ToChar() : "")}",

			_ => ToSan(move) // default → SAN
		};

	private static string ToSan(Move m)
	{
		// Castling is a complete SAN string by itself
		if ((m.CastleSide & CastleSide.King)  != 0) return "O-O";
		if ((m.CastleSide & CastleSide.Queen) != 0) return "O-O-O";

		var sb = new StringBuilder();

		// Piece letter (omit for pawns)
		if (m.PieceType != ChessPieceType.Pawn)
			sb.Append(m.PieceType.ToFenChar(m.MovingSide));

		// Capture marker and, for pawn captures, the origin file letter
		var isCapture = m.Kind is MoveKind.Capture or MoveKind.EnPassant or MoveKind.PromotionCapture;
		if (isCapture)
		{
			if (m.PieceType == ChessPieceType.Pawn)
			{
				// exd5 → 'e' == origin file letter
				var fileLetter = (char)('a' + m.From.Column);
				sb.Append(fileLetter);
			}

			sb.Append('x');
		}

		// Destination square
		sb.Append(m.To);

		// Promotion (e.g. "=Q")
		if (m.IsPromotion)
			sb.Append('=').Append(m.PromoteTo.ToChar());

		// Check / mate suffix
		if (m.IsCheckmate) sb.Append('#');
		else if (m.IsCheck) sb.Append('+');

		return sb.ToString();
	}
}
