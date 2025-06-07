using System;
using System.Collections.Generic;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Board.Models;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;
using Bezoro.Chess.Moves.Models;
using Bezoro.Chess.Pieces.Commands;
using Bezoro.Chess.Rules;

namespace Bezoro.Chess.Game.Models
{
	public class GameModel : IGameModel
	{
		/// <summary>
		///     Initializes a new game, optionally from a FEN string.
		///     Defaults to the standard chess starting position if no FEN is provided.
		/// </summary>
		/// <param name="fen">The FEN string to load the game state from. If null or empty, uses standard start.</param>
		/// <param name="boardWidth">The width of the chess board.</param>
		/// <param name="boardHeight">The height of the chess board.</param>
		/// <param name="rules">The rules of the game. If null uses the standard rules</param>
		public GameModel(string? fen = null, int boardWidth = 8, int boardHeight = 8, IGameRules? rules = null)
		{
			var setup = ResolveFen(fen);

			CastlingRights = setup.Castling;
			FullMoveNumber = setup.FullmoveNumber;
			HalfMoveClock  = setup.HalfmoveClock;
			ActiveColor    = setup.ActiveColor;
			Board          = new BoardModel(boardWidth, boardHeight, setup.PiecePlacement, setup.EnPassant);
			GameRules      = rules ?? new StandardChessRules();
			CapturedPieces = new(32); // Standard max captures
		}

		private readonly Stack<(GameStateMemento previousGameState, IChessCommand previousMoveCommand)> _undoHistory =
			new();

		public IGameRules             GameRules      { get; }
		public List<IChessPieceModel> CapturedPieces { get; }
		public CastlingRights         CastlingRights { get; internal set; }
		public IChessBoardModel       Board          { get; private set; }
		public int                    FullMoveNumber { get; internal set; }
		public int                    HalfMoveClock  { get; internal set; }
		public PlayerColor            ActiveColor    { get; internal set; }

	#region Interface Implementations

		/// <summary>
		///     Given an algebraic notation string, returns all legal moves the piece at that position can make.
		/// </summary>
		public (IChessPieceModel piece, IEnumerable<Move>) StartMove(string fromAlgebraic)
		{
			var from  = AlgebraicNotationUtils.FromAlgebraic(fromAlgebraic);
			var piece = Board.GetPieceAt(from);

			if (piece is null)
				throw new ArgumentException("No piece at the given position.", nameof(fromAlgebraic));

			var pseudoLegalMoves = piece.GetPseudoLegalMoves(this);
			var legalMoves       = GameRules.FilterLegalMoves(this, piece, pseudoLegalMoves);
			return (piece, legalMoves);
		}

		/// <summary>
		///     Generates the Forsyth-Edwards Notation (FEN) string for the current game state.
		/// </summary>
		/// <returns>The FEN string.</returns>
		public string ToFenString()
		{
			var piecePlacement  = Board.GetPiecePlacementFen();
			var enPassantString = Board.EnPassantTargetSquare.Position.Algebraic;

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

		public void DoMove(Move move)
		{
			var gameState = new GameStateMemento(
				ActiveColor, CastlingRights, Board.EnPassantTargetSquare, HalfMoveClock, FullMoveNumber);

			var moveCommand = new MovePieceCommand(move, Board);
			moveCommand.Execute(Board);
			ActiveColor = ActiveColor == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
			HalfMoveClock++;

			if (ActiveColor == PlayerColor.White)
				FullMoveNumber++;

			_undoHistory.Push((gameState, moveCommand));
		}

		public void SetBoard(IChessBoardModel board) =>
			Board = board;

		/// <summary>
		///     Undoes the last executed move.
		/// </summary>
		public void UndoLastMove()
		{
			if (_undoHistory.Count == 0)
				return;

			var (previousGameState, previousMove) = _undoHistory.Pop();

			previousMove.Undo(Board);
			Board.SetEnPassantTargetSquare(previousGameState.EnPassantTargetSquare);
			CastlingRights = previousGameState.CastlingRights;
			ActiveColor    = previousGameState.ActiveColor;
			HalfMoveClock  = previousGameState.HalfMoveClock;
			FullMoveNumber = previousGameState.FullMoveNumber;
		}

	#endregion

		private static BoardPosition? ResolveEnPassant(FenData setup) =>
			string.Equals(setup.EnPassant, "-", StringComparison.OrdinalIgnoreCase)
				? null
				: AlgebraicNotationUtils.FromAlgebraic(setup.EnPassant);

		private static FenData ResolveFen(string? fen) =>
			string.IsNullOrWhiteSpace(fen) ? FenUtils.StartBoard : FenUtils.Parse(fen);

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

	public interface IGameModel
	{
		CastlingRights         CastlingRights { get; }
		IChessBoardModel       Board          { get; }
		IGameRules             GameRules      { get; }
		int                    FullMoveNumber { get; }
		int                    HalfMoveClock  { get; }
		List<IChessPieceModel> CapturedPieces { get; }
		PlayerColor            ActiveColor    { get; }

		/// <summary>
		///     Given an algebraic notation string, returns all legal moves the piece at that position can make.
		/// </summary>
		(IChessPieceModel piece, IEnumerable<Move>) StartMove(string fromAlgebraic);

		/// <summary>
		///     Generates the Forsyth-Edwards Notation (FEN) string for the current game state.
		/// </summary>
		/// <returns>The FEN string.</returns>
		string ToFenString();

		void DoMove(Move move);
		void SetBoard(IChessBoardModel board);

		/// <summary>
		///     Undoes the last executed move.
		/// </summary>
		void UndoLastMove();
	}
}
