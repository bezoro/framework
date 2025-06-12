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
		public GameModel(string? fen = null, uint boardWidth = 8, uint boardHeight = 8, IGameRules? rules = null)
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

		public IGameRules             GameRules      { get; }
		public List<IChessPieceModel> CapturedPieces { get; }
		public CastlingRights         CastlingRights { get; private set; }
		public IChessBoardModel       Board          { get; private set; }
		public int                    FullMoveNumber { get; private set; }
		public int                    HalfMoveClock  { get; private set; }
		public PlayerColor            ActiveColor    { get; private set; }

		/// <summary>
		///     Given an algebraic notation string, returns all legal moves the piece at that position can make.
		/// </summary>
		public IEnumerable<Move> StartMove(string fromAlgebraic)
		{
			var from  = AlgebraicNotationUtils.FromAlgebraic(fromAlgebraic);
			var piece = Board.GetPieceAt(from);

			if (piece is null)
				throw new ArgumentException("No piece at the given position.", nameof(fromAlgebraic));

			var pseudoLegalMoves = piece.GetPseudoLegalMoves(this);
			var legalMoves       = GameRules.FilterLegalMoves(this, pseudoLegalMoves);
			return legalMoves;
		}

		/// <summary>
		///     Generates the Forsyth-Edwards Notation (FEN) string for the current game state.
		/// </summary>
		/// <returns>The FEN string.</returns>
		public string ToFenString() =>
			throw new NotImplementedException();

		public void AddCastlingRights(CastlingRights castlingRights) => CastlingRights |= castlingRights;

		public void CaptureAt(GameModel game, BoardPosition from, BoardPosition to)
		{
			var captureSquare = Board.GetSquareAt(to);
			var capturePiece  = captureSquare.Piece;

			CapturedPieces.Add(capturePiece);
			Board.MovePiece(from, to);
		}

		// TODO: Implement undo
		public void DoMove(Move move)
		{
			var moveCommand = new MovePieceCommand(move);
			moveCommand.Execute(this);
			ActiveColor = ActiveColor.Opposite();
			HalfMoveClock++;

			if (ActiveColor == PlayerColor.White)
				FullMoveNumber++;

			// TODO: Record commands for undo
		}

		public void RemoveCastlingRights(CastlingRights castlingRights) => CastlingRights &= ~castlingRights;

		public void RestoreLastCapturedPiece(ChessPieceType pieceTypeToRestore, BoardPosition restorePosition)
		{
			var capturedPiece = CapturedPieces.FindLast(x => x.GetPieceType() == pieceTypeToRestore);
			CapturedPieces.Remove(capturedPiece);
			Board.SetPieceAt(restorePosition, capturedPiece);
		}

		public void SetBoard(IChessBoardModel board) =>
			Board = board;

		public void SetCastlingRights(CastlingRights castlingRights) => CastlingRights = castlingRights;

		/// <summary>
		///     Undoes the last executed move.
		/// </summary>
		public void UndoLastMove() =>
			throw new NotImplementedException();

		private static FenData ResolveFen(string? fen) =>
			string.IsNullOrWhiteSpace(fen) ? FenUtils.StartBoard : FenUtils.Parse(fen);
	}

	public interface IGameModel { }
}
