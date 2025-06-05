using System;
using System.Collections.Generic;
using Bezoro.Core.Chess.Abstractions.Interfaces;
using Bezoro.Core.Chess.Board;
using Bezoro.Core.Chess.Board.Models;
using Bezoro.Core.Chess.Common.Enums;
using Bezoro.Core.Chess.Common.Extensions;
using Bezoro.Core.Chess.Common.Helpers;
using Bezoro.Core.Chess.Moves.Models;
using Bezoro.Core.Chess.Pieces.Commands;
using Bezoro.Core.Chess.Rules;

namespace Bezoro.Core.Chess.Game.Models
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
		public GameModel(string? fen = null, int boardWidth = 8, int boardHeight = 8, IGameRules? rules = null)
		{
			var setup = string.IsNullOrWhiteSpace(fen) ? FenUtils.StartBoard : FenUtils.Parse(fen);

			Board          = new(boardWidth, boardHeight, setup.PiecePlacement);
			GameRules      = rules ?? new StandardChessRules();
			CapturedPieces = new(32); // Standard max captures
			EnPassantTargetSquare = string.Equals(setup.EnPassant, "-", StringComparison.OrdinalIgnoreCase)
				? null
				: AlgebraicNotationUtils.FromAlgebraic(setup.EnPassant);

			CastlingRights = setup.Castling;
			FullMoveNumber = setup.FullmoveNumber;
			HalfMoveClock  = setup.HalfmoveClock;
			ActiveColor    = setup.ActiveColor;
		}

		public BoardModel             Board                 { get; }
		public IGameRules             GameRules             { get; }
		public List<IChessPieceModel> CapturedPieces        { get; }
		public BoardPosition?         EnPassantTargetSquare { get; internal set; }
		public CastlingRights         CastlingRights        { get; internal set; }
		public int                    FullMoveNumber        { get; internal set; }
		public int                    HalfMoveClock         { get; internal set; }
		public PlayerColor            ActiveColor           { get; internal set; }

		/// <summary>
		///     Attempts to execute a move defined by the move command.
		///     This method is responsible for validating the move in the context of game rules,
		///     updating the board state via BoardModel, and then updating the game state.
		/// </summary>
		/// <param name="moveCommand">The command detailing the move to be made.</param>
		/// <returns>True if the move was successful, false otherwise.</returns>
		public bool TryMovePiece(MovePieceCommand moveCommand)
		{
			moveCommand.Execute(Board);
			return true;
		}

		/// <summary>
		///     1) Asks the piece for all its pseudo‐legal moves,
		///     2) Filters them through the rule engine,
		///     3) Returns the actually legal moves.
		/// </summary>
		public IEnumerable<Move> StartMove(BoardPosition pos)
		{
			var pieceToMove = Board.GetPieceAt(pos);
			// 1. Generate all geometrically‐legal moves
			var pseudoMoves = pieceToMove.GetPseudoLegalMoves(this);

			// 2. Ask the rules engine to filter out illegal ones
			var legalMoves = GameRules.FilterLegalMoves(this, pieceToMove, pseudoMoves);

			return legalMoves;
		}

		/// <summary>
		///     Generates the Forsyth-Edwards Notation (FEN) string for the current game state.
		/// </summary>
		/// <returns>The FEN string.</returns>
		public string ToFenString()
		{
			var piecePlacement  = Board.GetPiecePlacementFen();
			var enPassantString = EnPassantTargetSquare?.Algebraic ?? "-";

			var currentFenData = new FenData(
				piecePlacement,
				ActiveColor,
				CastlingRights,
				enPassantString,
				HalfMoveClock,
				FullMoveNumber
			);

			return FenUtils.Format(currentFenData);
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
				// Simplified: Assumes standard board size for rook starting squares.
				// A more robust implementation might check actual initial rook positions if configurable.
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
			if (capturedPiece                   != null
				&& capturedPiece.GetPieceType() == ChessPieceType.Rook)
			{
				if (to.File == 0 && to.Rank == 0) // A1 was captured (White's QR)
					CastlingRights &= ~CastlingRights.WhiteQueenSide;
				else if (to.File == 7 && to.Rank == 0) // H1 was captured (White's KR)
					CastlingRights &= ~CastlingRights.WhiteKingSide;
				else if (to.File == 0 && to.Rank == 7) // A8 was captured (Black's QR)
					CastlingRights &= ~CastlingRights.BlackQueenSide;
				else if (to.File == 7 && to.Rank == 7) // H8 was captured (Black's KR)
					CastlingRights &= ~CastlingRights.BlackKingSide;
			}
		}
	}
}
