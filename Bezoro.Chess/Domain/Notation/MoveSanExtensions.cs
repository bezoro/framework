using System.Linq;
using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Moves;

namespace Bezoro.Chess.Domain.Notation
{
	/// <summary>
	///     Extension helpers that convert <see cref="Move" /> instances to Standard Algebraic Notation (SAN).
	/// </summary>
	public static class MoveSanExtensions
	{
		/// <summary>
		///     Returns fully-qualified SAN (captures, promotions, check, mate, castling).
		/// </summary>
		/// <param name="move">The move to format.</param>
		/// <param name="stateBeforeMove">Game state *before* <paramref name="move" /> is executed.</param>
		public static string ToSan(this Move move, GameState stateBeforeMove)
		{
			var san = BaseSan(move);

			// Produce the *after* position by using the existing immutable executor.
			var stateAfterMove = MoveExecution.ExecuteMove(stateBeforeMove, move);

			// After the move, it's the *other* side to move.
			var sideToMove = stateAfterMove.ActiveColor;

			if (!IsKingInCheck(stateAfterMove, sideToMove))
				return san; // nothing to append

			// Does the side to move have ANY legal reply that leaves its king safe?
			var hasEscape =
				MoveGenerator.GenerateMoves(stateAfterMove).Any(
					reply => !IsKingInCheck(MoveExecution.ExecuteMove(stateAfterMove, reply), sideToMove));

			return san + (hasEscape ? "+" : "#");
		}

		private static bool IsKingInCheck(GameState state, PieceColor kingColor)
		{
			var kingSquare = state.FindKingPosition(kingColor);
			return kingSquare is not null && state.IsSquareAttackedBy(kingSquare.Value, kingColor.Opposite());
		}

		private static string BaseSan(Move move)
		{
			if (move.Type == MoveType.CastleKingside) return "O-O";
			if (move.Type == MoveType.CastleQueenside) return "O-O-O";

			var destination = SquareToString(move.To);

			var pieceLetter = move.Piece.Type switch
			{
				PieceType.Knight => "N",
				PieceType.Bishop => "B",
				PieceType.Rook   => "R",
				PieceType.Queen  => "Q",
				PieceType.King   => "K",
				_                => string.Empty // pawn
			};

			var isCapture = move.Type is MoveType.Capture
										 or MoveType.EnPassant
										 or MoveType.PawnPromotionCapture;

			if (move.Piece.Type == PieceType.Pawn && isCapture)
			{
				// For pawn captures SAN starts with the file letter of origin.
				pieceLetter = ((char)('a' + move.From.Col)).ToString();
			}

			var captureMarker = isCapture ? "x" : string.Empty;
			var promotionSuffix = move.Type switch
			{
				MoveType.PawnPromotion or MoveType.PawnPromotionCapture =>
					"=" +
					PieceTypeToLetter(
						move.PromotionPiece == PieceType.None
							? PieceType.Queen // default
							: move.PromotionPiece),
				_ => string.Empty
			};

			return $"{pieceLetter}{captureMarker}{destination}{promotionSuffix}";
		}

		private static string PieceTypeToLetter(PieceType type) =>
			type switch
			{
				PieceType.Knight => "N",
				PieceType.Bishop => "B",
				PieceType.Rook   => "R",
				PieceType.Queen  => "Q",
				PieceType.King   => "K",
				_                => string.Empty
			};

		private static string SquareToString(Position pos)
		{
			var file = (char)('a' + pos.Col); // 0 → 'a'
			var rank = 8 - pos.Row;           // 0 → 8
			return $"{file}{rank}";
		}
	}
}
