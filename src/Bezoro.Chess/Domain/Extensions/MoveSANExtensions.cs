using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bezoro.Chess.Domain.Functions.Moves;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Extensions
{
	/// <summary>
	///     Extension helpers that convert <see cref="Move" /> instances to Standard Algebraic Notation (SAN).
	/// </summary>
	internal static class MoveSANExtensions
	{
		/// <summary>
		///     Returns fully qualified SAN (captures, promotions, check, mate, castling).
		/// </summary>
		/// <param name="move">The move to format.</param>
		/// <param name="stateBeforeMove">Game state *before* <paramref name="move" /> is executed.</param>
		public static string ToSAN(this Move move, GameState stateBeforeMove)
		{
			// The base SAN, e.g., "Nf3", "exd5", "O-O"
			var sanBuilder = new StringBuilder(BaseSAN(move, stateBeforeMove));

			// After the move is made, it's the other player's turn. We need to check if that player
			// is in check or checkmated to append the correct suffix ('+' or '#').
			GameState  stateAfterMove = MoveExecution.ExecuteMove(stateBeforeMove, move);
			PieceColor opponentColor  = stateAfterMove.ActiveColor;

			if (!IsKingInCheck(stateAfterMove, opponentColor))
			{
				return sanBuilder.ToString(); // No check, no suffix needed.
			}

			// The king is in check. Now, determine if it's checkmate.
			// Checkmate occurs if the opponent has NO legal moves that resolve the check.
			bool hasAnyLegalMove =
				MoveGenerator.GenerateMoves(stateAfterMove).Any(
					reply => !IsKingInCheck(MoveExecution.ExecuteMove(stateAfterMove, reply), opponentColor));

			sanBuilder.Append(hasAnyLegalMove ? "+" : "#");

			return sanBuilder.ToString();
		}

		private static bool IsKingInCheck(GameState state, PieceColor kingColor)
		{
			Position? kingSquare = state.FindKingPosition(kingColor);
			return kingSquare is not null && state.IsSquareAttackedBy(kingSquare.Value, kingColor.Opposite());
		}

		private static string BaseSAN(Move move, GameState state)
		{
			if (move.Type is MoveType.CastleKingside)
			{
				return "O-O";
			}

			if (move.Type is MoveType.CastleQueenside)
			{
				return "O-O-O";
			}

			string pieceLetter = PieceTypeToLetter(move.Piece.Type);
			bool   isPawnMove  = move.Piece.Type == PieceType.Pawn;
			bool   isCapture   = move.Type is MoveType.Capture or MoveType.EnPassant or MoveType.PromotionCapture;
			var    sanBuilder  = new StringBuilder();

			if (!isPawnMove)
			{
				sanBuilder.Append(pieceLetter);
				sanBuilder.Append(GetDisambiguation(move, state));
			}
			else if (isCapture)
			{
				// Pawn captures are prefixed with the file of origin, e.g., "exd5"
				sanBuilder.Append((char)('a' + move.From.Col));
			}

			if (isCapture)
			{
				sanBuilder.Append('x');
			}

			sanBuilder.Append(SquareToString(move.To));

			if (move.Type is not (MoveType.Promotion or MoveType.PromotionCapture))
			{
				return sanBuilder.ToString();
			}

			sanBuilder.Append('=');
			sanBuilder.Append(PieceTypeToLetter((PieceType)move.PromotionPieceType));

			return sanBuilder.ToString();
		}

		private static string GetDisambiguation(Move move, GameState state)
		{
			// Disambiguation is only needed for non-pawn moves.
			if (move.Piece.Type == PieceType.Pawn)
			{
				return string.Empty;
			}

			// Find all legal moves for the current player.
			IEnumerable<Move> allMoves = MoveGenerator.GenerateMoves(state);

			// Find moves by the same piece type to the same destination square.
			List<Move> ambiguousMoves = allMoves.Where(
				m =>
					m.Piece.Type == move.Piece.Type &&
					m.To         == move.To         &&
					m.From       != move.From).ToList();

			if (!ambiguousMoves.Any())
			{
				return string.Empty; // No other piece of this type can move here.
			}

			// Check if disambiguating by file is sufficient.
			bool sameFile = ambiguousMoves.Any(m => m.From.Col == move.From.Col);
			if (!sameFile)
			{
				return ((char)('a' + move.From.Col)).ToString();
			}

			// Files are the same, so we must disambiguate by rank.
			bool sameRank = ambiguousMoves.Any(m => m.From.Row == move.From.Row);
			if (!sameRank)
			{
				return (8 - move.From.Row).ToString();
			}

			// Both file and rank are the same (can happen with promotions, but we handle non-pawns).
			// This case is extremely rare but requires full coordinate disambiguation.
			return SquareToString(move.From);
		}

		private static string PieceTypeToLetter(PieceType type) =>
			type switch
			{
				PieceType.Knight => "N",
				PieceType.Bishop => "B",
				PieceType.Rook   => "R",
				PieceType.Queen  => "Q",
				PieceType.King   => "K",
				// Pawns are identified by the absence of a letter, except for captures.
				_ => string.Empty
			};

		private static string SquareToString(Position pos)
		{
			var file = (char)('a' + pos.Col); // Col 0 -> 'a'
			int rank = pos.Row + 1;           // Row 0 -> 8
			return $"{file}{rank}";
		}
	}
}
