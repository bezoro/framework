using System;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;

namespace Bezoro.Chess.Pieces.Commands
{
	public sealed class MovePieceCommand : IChessCommand
	{
		/// <summary>
		///     Creates a command that moves the piece described by <paramref name="move" />.
		///     The board instance is needed only for looking up the squares – it is not
		///     modified in the constructor.
		/// </summary>
		public MovePieceCommand(Move move)
		{
			Move = move;
		}

		internal Move          Move                  { get; }
		internal CaptureData   PreviousCaptureData   { get; private set; }
		internal CastlingData  PreviousCastlingData  { get; private set; }
		internal PromotionData PreviousPromotionData { get; private set; }

	#region Interface Implementations

		public void Execute(GameModel game)
		{
			var board       = game.Board;
			var pieceToMove = board.GetPieceAt(Move.From);

			if (pieceToMove is null)
				throw new InvalidOperationException("Trying to move a Piece that is null.");

			switch (Move.Kind)
			{
				case MoveKind.Normal:
					PerformPieceMovement(board, pieceToMove);
					break;
				case MoveKind.Capture:
					PerformPieceCapture(game, board, pieceToMove);
					break;
				case MoveKind.EnPassant:
					PerformEnPassant(game, board, pieceToMove);
					break;
				case MoveKind.PromotionQuiet:
					PreviousPromotionData = new(Move.To, Move.PromoteTo);
					board.MovePieceTo(pieceToMove, Move.From, Move.To);
					pieceToMove.MarkMoved();
					break;
				case MoveKind.PromotionCapture:
					PreviousPromotionData = new(Move.To, Move.PromoteTo);
					PerformPieceCapture(game, board, pieceToMove);
					break;
				case MoveKind.Castle:
					if (Move.PieceType != ChessPieceType.King)
						throw new InvalidOperationException("Trying to castle a non-king piece.");

					PreviousCastlingData = new(pieceToMove, Move.CastleSide);
					board.PerformCastle(pieceToMove, Move.CastleSide);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public void Undo(GameModel game)
		{
			var board           = game.Board;
			var pieceToUndoMove = board.GetPieceAt(Move.To);

			var capturedPosition  = PreviousCaptureData.Position;
			var capturedPieceType = PreviousCaptureData.PieceState.PieceType;

			switch (Move.Kind)
			{
				case MoveKind.Normal:
					board.MovePieceTo(pieceToUndoMove, Move.To, Move.From);
					pieceToUndoMove.ResetMoved();
					break;
				case MoveKind.Capture:
					board.MovePieceTo(pieceToUndoMove, Move.To, Move.From);
					board.RestoreLastCapturedPiece(capturedPieceType, capturedPosition, game);
					pieceToUndoMove.ResetMoved();
					break;
				case MoveKind.EnPassant:
					board.MovePieceTo(pieceToUndoMove, Move.To, Move.From);
					board.RestoreLastCapturedPiece(capturedPieceType, capturedPosition, game);
					board.SetEnPassantTargetSquare(PreviousCaptureData.EnPassant);
					pieceToUndoMove.ResetMoved();
					break;
				case MoveKind.PromotionQuiet:
					break;
				case MoveKind.PromotionCapture:
					break;
				case MoveKind.Castle:
					break;
				case MoveKind.CastleKingside:
					break;
				case MoveKind.CastleQueenside:
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

	#endregion

		private void PerformEnPassant(GameModel game, IChessBoardModel board, IChessPieceModel pieceToMove)
		{
			var enPassantSquare = board.EnPassantTargetSquare;
			var capturablePiecePosition = new BoardPosition(
				enPassantSquare.Position.Column, enPassantSquare.Position.Row - 1);

			var pieceToCapture = board.GetPieceAt(capturablePiecePosition);
			PreviousCaptureData = new(pieceToCapture, capturablePiecePosition, enPassantSquare);

			board.CapturePieceAt(pieceToCapture, capturablePiecePosition, game);
			board.MovePieceTo(pieceToMove, Move.From, Move.To);
			pieceToMove.MarkMoved();
		}

		private void PerformPieceCapture(GameModel game, IChessBoardModel board, IChessPieceModel pieceToMove)
		{
			var pieceToCapture = board.GetPieceAt(Move.To);

			if (pieceToCapture is null)
				throw new InvalidOperationException("Trying to capture a Piece that is null.");

			PreviousCaptureData = new(pieceToCapture, Move.To);
			board.CapturePieceAt(pieceToCapture, Move.To, game);
			board.MovePieceTo(pieceToMove, Move.From, Move.To);
			pieceToMove.MarkMoved();
		}

		private void PerformPieceMovement(IChessBoardModel board, IChessPieceModel pieceToMove)
		{
			board.MovePieceTo(pieceToMove, Move.From, Move.To);
			pieceToMove.MarkMoved();
		}
	}

	/// <summary>
	///     Stores information about a captured piece for undo purposes.
	/// </summary>
	internal readonly struct CaptureData
	{
		internal CaptureData(IChessPieceModel piece, BoardPosition position, IChessBoardSquareModel? enPassant = null)
		{
			PieceState = new(piece);
			Position   = position;
			EnPassant  = enPassant;
		}

		public   IChessBoardSquareModel? EnPassant  { get; }
		internal BoardPosition           Position   { get; }
		internal PieceState              PieceState { get; }
	}

	/// <summary>
	///     Stores information about the rook involved in a castling move for undo purposes.
	/// </summary>
	internal readonly struct CastlingData
	{
		internal CastlingData(IChessPieceModel rook, CastleSide side)
		{
			RookState = new(rook);
			switch (side)
			{
				case CastleSide.King:
					RookOriginalPosition = new("h1");
					RookTargetPosition   = new("g1");
					break;
				case CastleSide.Queen:
					RookOriginalPosition = new("a1");
					RookTargetPosition   = new("c1");
					break;
				default:
					RookOriginalPosition = null;
					RookTargetPosition   = null;
					break;
			}
		}

		internal BoardPosition? RookOriginalPosition { get; }
		internal BoardPosition? RookTargetPosition   { get; }
		internal PieceState     RookState            { get; }
	}

	/// <summary>
	///     Stores the essential state of a piece before a move, for undo purposes.
	/// </summary>
	internal readonly struct PieceState
	{
		internal PieceState(IChessPieceModel piece)
		{
			PieceType = piece.GetPieceType();
			HasMoved  = piece.HasMoved;
		}

		internal bool           HasMoved  { get; }
		internal ChessPieceType PieceType { get; }
	}

	/// <summary>
	///     Stores information about a pawn promotion.
	/// </summary>
	internal readonly struct PromotionData
	{
		internal PromotionData(BoardPosition position, PromotionPieceType promotedToType)
		{
			Position      = position;
			PromotionType = promotedToType;
		}

		internal BoardPosition      Position      { get; }
		internal bool               IsValid       => PromotionType != PromotionPieceType.None;
		internal PromotionPieceType PromotionType { get; }
	}
}
