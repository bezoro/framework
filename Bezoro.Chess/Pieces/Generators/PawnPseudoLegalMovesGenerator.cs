using System;
using System.Collections.Generic;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.Pieces.Generators
{
	/// <summary>
	///     Generates pseudo-legal moves for a pawn chess piece.
	///     Pseudo-legal moves are all possible moves without considering if they put the king in check.
	///     Emits pawn moves considering board occupancy and en-passant rules.
	///     The white template is mirrored for black by multiplying
	///     <c>dy</c> with <c>pawn.Direction</c> (+1 / −1).
	///     Higher layers handle check-legality.
	///     Pawn movement rules:
	///     - Can move forward one square if unblocked
	///     - Can move forward two squares on first move if unblocked
	///     - Can capture diagonally one square forward
	///     - Can capture en passant when conditions are met
	///     - Must promote when reaching opposite end of board
	/// </summary>
	public sealed class PawnPseudoLegalMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		/// <summary>
		///     Generates all pseudo-legal moves for a pawn piece in the current game state.
		/// </summary>
		/// <param name="game">The current game state containing the board position.</param>
		/// <param name="piece">The pawn piece to generate moves for.</param>
		/// <returns>An enumerable collection of possible moves for the pawn.</returns>
		/// <exception cref="ArgumentNullException">Thrown when game or piece is null.</exception>
		/// <exception cref="ArgumentException">Thrown when the piece is not a pawn or not on the board.</exception>
		public IEnumerable<Move> Generate(GameModel game, IChessPieceModel piece)
		{
			if (game is null) throw new ArgumentNullException(nameof(game));
			if (piece is null) throw new ArgumentNullException(nameof(piece));

			var board = game.Board ?? throw new ArgumentException("Game.Board cannot be null.", nameof(game));
			var pawn  = EnsurePawnPiece(piece);

			var from = board.GetPosition(pawn);
			if (from is null) throw new ArgumentException("Pawn is not on the board.", nameof(piece));

			foreach (var move in GenerateMoves(game, pawn, from.Value))
			{
				yield return move;
			}
		}

	#endregion

		/// <summary>
		///     Determines if the given rank is a promotion rank for the specified color.
		/// </summary>
		/// <param name="color">The color of the pawn.</param>
		/// <param name="rank">The rank to check.</param>
		/// <param name="board">The chess board.</param>
		/// <returns>True if the rank is a promotion rank, false otherwise.</returns>
		private static bool IsPromotionRank(PlayerColor color, int rank, IChessBoardModel board) =>
			color == PlayerColor.White ? rank == board.Height - 1 : rank == 0;

		/// <summary>
		///     Builds move objects for normal moves or promotions based on the target square.
		/// </summary>
		/// <param name="from">Starting position of the move.</param>
		/// <param name="to">Target position of the move.</param>
		/// <param name="pawn">The pawn making the move.</param>
		/// <param name="kind">The kind of move being made (before considering promotion, e.g., Normal or Capture).</param>
		/// <param name="board">The chess board.</param>
		/// <returns>Collection of possible moves including promotions if applicable.</returns>
		private static IEnumerable<Move> BuildMoves(
			BoardPosition from,
			BoardPosition to,
			PawnModel pawn,
			MoveKind kind,
			IChessBoardModel board)
		{
			// Check for promotion
			// En Passant cannot result in promotion (this is also checked in TryGenerateEnPassant before calling BuildMoves)
			if (kind != MoveKind.EnPassant && IsPromotionRank(pawn.Color, to.Row, board))
			{
				var promotionTypes = new[]
				{
					PromotionPieceType.Queen,
					PromotionPieceType.Rook,
					PromotionPieceType.Bishop,
					PromotionPieceType.Knight
				};

				foreach (var promotionType in promotionTypes)
				{
					if (kind == MoveKind.Capture)
					{
						// Assumes Move.PromotionCapture(from, to, color, promotionType) exists
						// and sets the move's Kind to MoveKind.PromotionCapture.
						yield return Move.PromotionCapture(from, to, pawn.Color, promotionType);
					}
					else // kind would be MoveKind.Normal for quiet pushes resulting in promotion
					{
						// Assumes Move.PromotionQuiet(from, to, color, promotionType)
						// sets the move's Kind to MoveKind.PromotionQuiet.
						yield return Move.PromotionQuiet(from, to, pawn.Color, promotionType);
					}
				}
			}
			// Normal move (or EnPassant, which is not a promotion itself)
			else
			{
				yield return Move.Standard(from, to, pawn.Color, pawn.GetPieceType(), kind);
			}
		}

		/// <summary>
		///     Generates all possible pawn moves from a given position.
		/// </summary>
		/// <param name="game">The current game state.</param>
		/// <param name="pawn">The pawn to generate moves for.</param>
		/// <param name="from">The pawn's current position.</param>
		/// <returns>Collection of all possible moves for the pawn.</returns>
		private static IEnumerable<Move> GenerateMoves(GameModel game, PawnModel pawn, BoardPosition from)
		{
			var board         = game.Board;
			var pawnDirection = pawn.Direction; // Pawn's forward direction (+1 for White, -1 for Black)

			// Generate single push moves
			var singlePushMove = TryGenerateSinglePush(pawn, from, board, pawnDirection);
			if (singlePushMove != null)
			{
				foreach (var move in singlePushMove)
				{
					yield return move;
				}

				// Only try double push if single push is possible and pawn hasn't moved
				if (!pawn.HasMoved)
				{
					var doublePushMoves = TryGenerateDoublePush(pawn, from, board, pawnDirection);
					if (doublePushMoves != null)
					{
						foreach (var move in doublePushMoves)
						{
							yield return move;
						}
					}
				}
			}

			// Generate capture moves
			foreach (var dx in new[] { -1, 1 }) // Left and right captures
			{
				// Try regular capture
				var captureTargetFile = from.Column + dx;
				var captureTargetRank = from.Row    + pawnDirection;

				if (board.IsInside(captureTargetFile, captureTargetRank))
				{
					var captureTo   = new BoardPosition(captureTargetFile, captureTargetRank);
					var targetPiece = board.Squares[captureTargetFile, captureTargetRank].GetPiece();

					// If there's an enemy piece to capture
					if (targetPiece != null && targetPiece.Color != pawn.Color)
					{
						foreach (var move in BuildMoves(from, captureTo, pawn, MoveKind.Capture, board))
						{
							yield return move;
						}
					}
				}

				// Try en passant
				var enPassantMoves = TryGenerateEnPassant(pawn, from, board, pawnDirection, dx);
				if (enPassantMoves != null)
				{
					foreach (var move in enPassantMoves)
					{
						yield return move;
					}
				}
			}
		}

		/// <summary>
		///     Attempts to generate a double push move for a pawn.
		/// </summary>
		/// <param name="pawn">The pawn to move.</param>
		/// <param name="from">Starting position.</param>
		/// <param name="board">The chess board.</param>
		/// <param name="pawnDir">Direction of pawn movement.</param>
		/// <returns>A collection of double push moves if valid, null otherwise.</returns>
		private static IEnumerable<Move> TryGenerateDoublePush(
			PawnModel pawn,
			BoardPosition from,
			IChessBoardModel board,
			int pawnDir)
		{
			var targetFile = from.Column;
			var targetRank = from.Row + 2 * pawnDir;

			// Check if target is inside board
			if (!board.IsInside(targetFile, targetRank))
				return null;

			// Check if target square is empty
			var targetSquare = board.Squares[targetFile, targetRank];
			if (targetSquare.IsOccupied)
				return null;

			// Check if the intermediate square is empty
			var intermediateRank   = from.Row + pawnDir;
			var intermediateSquare = board.Squares[targetFile, intermediateRank];
			if (intermediateSquare.IsOccupied)
				return null;

			var to = new BoardPosition(targetFile, targetRank);
			return BuildMoves(from, to, pawn, MoveKind.Normal, board);
		}

		/// <summary>
		///     Attempts to generate an en passant capture move for a pawn.
		/// </summary>
		/// <param name="pawn">The pawn to move.</param>
		/// <param name="from">Starting position.</param>
		/// <param name="board">The chess board.</param>
		/// <param name="pawnDir">Direction of pawn movement.</param>
		/// <param name="dx">Horizontal direction of capture.</param>
		/// <returns>A collection of en passant moves if valid, null otherwise.</returns>
		private static IEnumerable<Move> TryGenerateEnPassant(
			PawnModel pawn,
			BoardPosition from,
			IChessBoardModel board,
			int pawnDir,
			int dx)
		{
			var targetFile = from.Column + dx;
			var targetRank = from.Row    + pawnDir;

			// Check if target is inside board
			if (!board.IsInside(targetFile, targetRank))
				return null;

			var to = new BoardPosition(targetFile, targetRank);

			// Check if target is a promotion rank (en passant can't result in promotion)
			if (IsPromotionRank(pawn.Color, targetRank, board))
				return null;

			// Target square must be empty for en passant
			if (board.Squares[targetFile, targetRank].IsOccupied)
				return null;

			// Must have a valid en passant target square
			if (board.EnPassantTargetSquare == null || !board.EnPassantTargetSquare.Position.Equals(to))
				return null;

			// Check for the captured pawn (must be on the same rank as the attacking pawn)
			var capturedPawnFile = targetFile;
			var capturedPawnRank = from.Row;
			var capturedPawn     = board.Squares[capturedPawnFile, capturedPawnRank].Piece;

			// Verify there's an enemy pawn to capture
			if (capturedPawn                == null                ||
				capturedPawn.GetPieceType() != ChessPieceType.Pawn ||
				capturedPawn.Color          == pawn.Color)
				return null;

			return BuildMoves(from, to, pawn, MoveKind.EnPassant, board);
		}

		/// <summary>
		///     Attempts to generate a single push move for a pawn.
		/// </summary>
		/// <param name="pawn">The pawn to move.</param>
		/// <param name="from">Starting position.</param>
		/// <param name="board">The chess board.</param>
		/// <param name="pawnDir">Direction of pawn movement.</param>
		/// <returns>A collection of single push moves if valid, null otherwise.</returns>
		private static IEnumerable<Move> TryGenerateSinglePush(
			PawnModel pawn,
			BoardPosition from,
			IChessBoardModel board,
			int pawnDir)
		{
			var targetFile = from.Column;
			var targetRank = from.Row + pawnDir;

			// Check if target is inside board
			if (!board.IsInside(targetFile, targetRank))
				return null;

			var to           = new BoardPosition(targetFile, targetRank);
			var targetSquare = board.Squares[targetFile, targetRank];

			// Square must be empty for a push
			if (targetSquare.IsOccupied)
				return null;

			return BuildMoves(from, to, pawn, MoveKind.Normal, board);
		}

		/// <summary>
		///     Validates that the piece is a pawn.
		/// </summary>
		/// <param name="piece">The chess piece to validate.</param>
		/// <returns>The validated pawn piece.</returns>
		/// <exception cref="ArgumentException">Thrown when piece is not a pawn.</exception>
		private static PawnModel EnsurePawnPiece(IChessPieceModel piece)
		{
			if (piece is not PawnModel pawn)
				throw new ArgumentException("Generator received a non-pawn piece.", nameof(piece));

			return pawn;
		}
	}
}
