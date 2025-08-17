using Bezoro.Chess.Domain.Functions.Moves.Generation;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Structs.Moves;

namespace Bezoro.Chess.Domain.Extensions
{
	internal static class MoveExtensions
	{
		/// <summary>
		///     Converts a normal move to base algebraic notation without disambiguation.
		///     For pawns, only includes the destination square. For other pieces, includes the piece letter and destination
		///     square.
		/// </summary>
		/// <param name="move">The normal move to convert.</param>
		/// <returns>The move in base algebraic notation without disambiguation.</returns>
		public static string ToBaseAlgebraicNotation(this NormalMove move) =>
			move.MovingPiece.Type == PieceType.Pawn
				? move.To.Coordinate.ToString().ToLower()
				: $"{move.MovingPiece.Type.ToFENChar(PieceColor.White)}{move.To.Coordinate.ToString().ToLower()}";

		/// <summary>
		///     Converts a castling move to base algebraic notation.
		///     Returns "O-O" for kingside castling and "O-O-O" for queenside castling.
		/// </summary>
		/// <param name="move">The castle move to convert.</param>
		/// <returns>The move in base algebraic notation.</returns>
		public static string ToBaseAlgebraicNotation(this CastleMove move) =>
			move.Side == CastlingSide.Kingside ? "O-O" : "O-O-O";

		/// <summary>
		///     Converts an en passant capture move to base algebraic notation without disambiguation.
		///     Includes the file of origin, capture symbol 'x', destination square, and 'e.p.' suffix.
		/// </summary>
		/// <param name="move">The en passant move to convert.</param>
		/// <returns>The move in base algebraic notation without disambiguation.</returns>
		public static string ToBaseAlgebraicNotation(this EnPassantMove move) =>
			$"{move.From.Coordinate.ToString()[0]}x{move.To.Coordinate.ToString().ToLower()} e.p.";

		/// <summary>
		///     Converts a pawn promotion move to base algebraic notation without disambiguation.
		///     Includes the destination square and the promotion piece letter prefixed with '='.
		/// </summary>
		/// <param name="move">The promotion move to convert.</param>
		/// <returns>The move in base algebraic notation without disambiguation.</returns>
		public static string ToBaseAlgebraicNotation(this PromotionMove move) =>
			$"{move.To.Coordinate.ToString().ToLower()}={move.PromotionPiece.Type.ToFENChar(PieceColor.White)}";

		/// <summary>
		///     Converts a capture move to base algebraic notation without disambiguation.
		///     For pawns, includes the file of origin. For other pieces, includes the piece letter.
		///     Always includes the capture symbol 'x' and destination square.
		/// </summary>
		/// <param name="move">The capture move to convert.</param>
		/// <returns>The move in base algebraic notation without disambiguation.</returns>
		public static string ToBaseAlgebraicNotation(this CaptureMove move)
		{
			string prefix = move.MovingPiece.Type == PieceType.Pawn
				? move.From.Coordinate.ToString()[0].ToString()
				: move.MovingPiece.Type.ToFENChar(PieceColor.White).ToString();

			return $"{prefix}x{move.To.Coordinate.ToString().ToLower()}";
		}
	}
}
