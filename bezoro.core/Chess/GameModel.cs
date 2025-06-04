using System;
using System.Collections.Generic;
using System.Text;
using Bezoro.Core.Chess.Interfaces;
using Bezoro.Core.Chess.Utils;

// For FenData, FenUtility, AlgebraicNotationUtils, ChessUtils

namespace Bezoro.Core.Chess
{
	public class GameModel
	{
		/// <summary>
		///     Initializes a new game, optionally from a FEN string.
		///     Defaults to the standard chess starting position if no FEN is provided.
		/// </summary>
		/// <param name="fen">The FEN string to load the game state from. If null or empty, uses standard start.</param>
		/// <param name="boardWidth">The width of the chess board.</param>
		/// <param name="boardHeight">The height of the chess board.</param>
		public GameModel(string? fen = null, int boardWidth = 8, int boardHeight = 8)
		{
			var setup = string.IsNullOrWhiteSpace(fen) ? FenUtility.StartBoard : FenUtility.Parse(fen);

			Board          = new(boardWidth, boardHeight, setup.PiecePlacement);
			CapturedPieces = new(32); // Standard max captures

			ActiveColor    = setup.ActiveColor;
			CastlingRights = setup.Castling;
			EnPassantTargetSquare = string.Equals(setup.EnPassant, "-", StringComparison.OrdinalIgnoreCase)
				? null
				: AlgebraicNotationUtils.FromAlgebraic(setup.EnPassant); // Assuming this utility exists

			HalfMoveClock  = setup.HalfmoveClock;
			FullMoveNumber = setup.FullmoveNumber;
		}

		public BoardModel             Board                 { get; }
		public List<IChessPieceModel> CapturedPieces        { get; }
		public BoardPosition?         EnPassantTargetSquare { get; internal set; }
		public CastlingRights         CastlingRights        { get; internal set; }
		public int                    FullMoveNumber        { get; internal set; }
		public int                    HalfMoveClock         { get; internal set; }

		public PlayerColor ActiveColor { get; internal set; }

		/// <summary>
		///     Attempts to execute a move defined by the move command.
		///     This method is responsible for validating the move in the context of game rules,
		///     updating the board state via BoardModel, and then updating the game state.
		/// </summary>
		/// <param name="moveCommand">The command detailing the move to be made.</param>
		/// <returns>True if the move was successful, false otherwise.</returns>
		public bool TryMovePiece(MovePieceCommand moveCommand)
		{
			if (moveCommand == null)
				throw new ArgumentNullException(nameof(moveCommand));

			var pieceToMove = Board.GetPieceAt(moveCommand.From.Position);

			if (pieceToMove       == null) return false;        // No piece at the source square
			if (pieceToMove.Color != ActiveColor) return false; // Not the active player's piece

			// TODO: Add comprehensive move validation here:
			// 1. Piece-specific move logic (e.g., can a knight move like that?)
			// 2. Path clear? (for sliders)
			// 3. Target square valid? (e.g., not own piece unless castling)
			// 4. Does the move leave the king in check?

			var capturedPiece = Board.GetPieceAt(moveCommand.To.Position); // Check for capture before moving

			// Execute the move on the board
			Board.MovePieceInternal(pieceToMove, moveCommand.From.Position, moveCommand.To.Position);

			// Update game state based on the move
			if (capturedPiece != null && capturedPiece.Color != pieceToMove.Color)
			{
				CapturedPieces.Add(capturedPiece);
				HalfMoveClock = 0; // Reset halfmove clock on capture
			}
			else if (pieceToMove.GetPieceType() == ChessPieceType.Pawn) // Assuming IChessPieceModel has Type
			{
				HalfMoveClock = 0; // Reset halfmove clock on pawn move
			}
			else
			{
				HalfMoveClock++;
			}

			// Update Castling Rights (simplified version)
			UpdateCastlingRightsOnMove(pieceToMove, moveCommand.From.Position, capturedPiece, moveCommand.To.Position);

			// Set En Passant Target Square (simplified version)
			if (pieceToMove.GetPieceType()                                                 == ChessPieceType.Pawn
				&& Math.Abs(moveCommand.From.Position.Rank - moveCommand.To.Position.Rank) == 2)
			{
				// Pawn moved two squares, set en passant target
				var enPassantRank = (moveCommand.From.Position.Rank + moveCommand.To.Position.Rank) / 2;
				EnPassantTargetSquare = new BoardPosition(moveCommand.From.Position.File, enPassantRank);
			}
			else
			{
				EnPassantTargetSquare = null; // Clear en passant otherwise
			}

			// Toggle active color
			ActiveColor = ActiveColor == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;

			// Increment fullmove number if Black just moved (meaning it's now White's turn)
			if (ActiveColor == PlayerColor.White)
			{
				FullMoveNumber++;
			}

			// The BoardModel itself handles invalidating its snapshot internally when MovePieceInternal is called.
			return true;
		}

		/// <summary>
		///     Generates the Forsyth-Edwards Notation (FEN) string for the current game state.
		/// </summary>
		/// <returns>The FEN string.</returns>
		public string ToFenString()
		{
			var fen = new StringBuilder();

			// 1. Piece placement (from BoardModel)
			fen.Append(Board.GetPiecePlacementFen()); // BoardModel will provide this part
			fen.Append(' ');

			// 2. Active color
			fen.Append(ActiveColor == PlayerColor.White ? 'w' : 'b');
			fen.Append(' ');

			// 3. Castling availability
			var castlingStr = new StringBuilder();
			if (CastlingRights.HasFlag(CastlingRights.WhiteKingSide)) castlingStr.Append('K');
			if (CastlingRights.HasFlag(CastlingRights.WhiteQueenSide)) castlingStr.Append('Q');
			if (CastlingRights.HasFlag(CastlingRights.BlackKingSide)) castlingStr.Append('k');
			if (CastlingRights.HasFlag(CastlingRights.BlackQueenSide)) castlingStr.Append('q');
			fen.Append(castlingStr.Length > 0 ? castlingStr.ToString() : "-");
			fen.Append(' ');

			// 4. En passant target square
			// Assuming BoardPosition has an 'Algebraic' property or similar representation
			fen.Append(EnPassantTargetSquare?.Algebraic ?? "-");
			fen.Append(' ');

			// 5. Halfmove clock
			fen.Append(HalfMoveClock);
			fen.Append(' ');

			// 6. Fullmove number
			fen.Append(FullMoveNumber);

			return fen.ToString();
		}

		private void UpdateCastlingRightsOnMove(
			IChessPieceModel movedPiece,
			BoardPosition from,
			IChessPieceModel? capturedPiece,
			BoardPosition to)
		{
			// If King moved
			if (movedPiece.GetPieceType() == ChessPieceType.King)
			{
				if (movedPiece.Color == PlayerColor.White)
				{
					CastlingRights &= ~(CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide);
				}
				else // Black King
				{
					CastlingRights &= ~(CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide);
				}
			}
			// If Rook moved from its starting square
			else if (movedPiece.GetPieceType() == ChessPieceType.Rook)
			{
				if (movedPiece.Color == PlayerColor.White)
				{
					if (from.File == 0 && from.Rank == 0) // A1
						CastlingRights &= ~CastlingRights.WhiteQueenSide;
					else if (from.File == 7 && from.Rank == 0) // H1
						CastlingRights &= ~CastlingRights.WhiteKingSide;
				}
				else // Black Rook
				{
					if (from.File == 0 && from.Rank == 7) // A8
						CastlingRights &= ~CastlingRights.BlackQueenSide;
					else if (from.File == 7 && from.Rank == 7) // H8
						CastlingRights &= ~CastlingRights.BlackKingSide;
				}
			}

			// If a rook is captured on its starting square
			if (capturedPiece != null && capturedPiece.GetPieceType() == ChessPieceType.Rook)
			{
				if (to.File == 0 && to.Rank == 0) // A1 was captured
					CastlingRights &= ~CastlingRights.WhiteQueenSide;
				else if (to.File == 7 && to.Rank == 0) // H1 was captured
					CastlingRights &= ~CastlingRights.WhiteKingSide;
				else if (to.File == 0 && to.Rank == 7) // A8 was captured
					CastlingRights &= ~CastlingRights.BlackQueenSide;
				else if (to.File == 7 && to.Rank == 7) // H8 was captured
					CastlingRights &= ~CastlingRights.BlackKingSide;
			}
		}
	}
}
