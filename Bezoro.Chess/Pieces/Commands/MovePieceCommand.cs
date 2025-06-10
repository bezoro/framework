using System;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;
using Bezoro.Chess.Pieces.Models;

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

		internal Move          Move                     { get; }
		internal CaptureData   PreviousCaptureData      { get; private set; }
		internal CastlingData  PreviousCastlingData     { get; private set; }
		internal PieceState    PreviousMovingPieceState { get; private set; }
		internal PromotionData PreviousPromotionData    { get; private set; }

	#region Interface Implementations

		public void Execute(GameModel game)
		{
			var board           = game.Board;
			var pieceToMove     = board.GetPieceAt(Move.From);
			var promotionSquare = board.GetSquareAt(Move.To);
			var pawn            = pieceToMove as PawnModel;

			if (pieceToMove is null)
				throw new InvalidOperationException("Trying to move a Piece that is null.");

			switch (Move.Kind)
			{
				case MoveKind.Normal:
					PreviousMovingPieceState = new(pieceToMove);
					PerformPieceMovement(board, pieceToMove);
					break;
				case MoveKind.Capture:
					PreviousMovingPieceState = new(pieceToMove);
					PerformPieceCapture(game, board, pieceToMove);
					break;
				case MoveKind.EnPassant:
					PreviousMovingPieceState = new(pieceToMove);
					PerformEnPassant(game, board, pieceToMove, Move.MovingSide);
					break;
				case MoveKind.PromotionQuiet:
					PreviousMovingPieceState = new(pieceToMove);
					PreviousPromotionData    = new(Move.To, Move.PromoteTo);
					board.MovePieceTo(pawn, Move.From, Move.To);
					pawn.PromoteTo(promotionSquare, Move.PromoteTo);
					break;
				case MoveKind.PromotionCapture:
					PreviousMovingPieceState = new(pieceToMove);
					PreviousPromotionData    = new(Move.To, Move.PromoteTo);
					PerformPieceCapture(game, board, pieceToMove);
					pawn.PromoteTo(promotionSquare, Move.PromoteTo);
					break;
				case MoveKind.Castle:
					if (Move.PieceType != ChessPieceType.King)
						throw new InvalidOperationException("Trying to castle a non-king piece.");

					var king = pieceToMove;
					var rookPosition = Move.CastleSide == CastleSide.King
						? Move.MovingSide == PlayerColor.White ? "h1" : "h8"
						: Move.MovingSide == PlayerColor.White ? "a1" : "a8";

					var rook = board.GetPieceAt(rookPosition);

					PreviousMovingPieceState = new(king);
					PreviousCastlingData     = new(king, rook, Move.CastleSide, Move.MovingSide);
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
					pieceToUndoMove.SetMoved(PreviousMovingPieceState.HasMoved);
					break;
				case MoveKind.Capture:
					board.MovePieceTo(pieceToUndoMove, Move.To, Move.From);
					board.RestoreLastCapturedPiece(capturedPieceType, capturedPosition, game);
					pieceToUndoMove.SetMoved(PreviousMovingPieceState.HasMoved);
					break;
				case MoveKind.EnPassant:
					board.MovePieceTo(pieceToUndoMove, Move.To, Move.From);
					board.RestoreLastCapturedPiece(capturedPieceType, capturedPosition, game);
					board.SetEnPassantTargetSquare(PreviousCaptureData.EnPassant);
					pieceToUndoMove.SetMoved(PreviousMovingPieceState.HasMoved);
					break;
				case MoveKind.PromotionQuiet:
					pieceToUndoMove = board.CreatePieceAt(Move.To, Move.MovingSide, Move.PieceType);
					board.MovePieceTo(pieceToUndoMove, Move.To, Move.From);
					pieceToUndoMove.SetMoved(PreviousMovingPieceState.HasMoved);
					break;
				case MoveKind.PromotionCapture:
					pieceToUndoMove = board.CreatePieceAt(Move.To, Move.MovingSide, Move.PieceType);
					board.MovePieceTo(pieceToUndoMove, Move.To, Move.From);
					pieceToUndoMove.SetMoved(PreviousMovingPieceState.HasMoved);
					board.RestoreLastCapturedPiece(capturedPieceType, capturedPosition, game);
					break;
				case MoveKind.Castle:
					board.MovePieceTo(pieceToUndoMove, Move.To, Move.From);
					pieceToUndoMove.SetMoved(PreviousCastlingData.KingState.HasMoved);

					if (PreviousCastlingData is { RookOriginalPosition: not null, RookTargetPosition: not null })
					{
						var rook = board.GetPieceAt(PreviousCastlingData.RookTargetPosition.Value);
						board.MovePieceTo(
							rook, PreviousCastlingData.RookTargetPosition.Value,
							PreviousCastlingData.RookOriginalPosition.Value);

						rook.SetMoved(PreviousCastlingData.RookState.HasMoved);
					}

					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

	#endregion

		private void PerformEnPassant(
			GameModel game,
			IChessBoardModel board,
			IChessPieceModel pieceToMove,
			PlayerColor color)
		{
			var dir             = color == PlayerColor.White ? 1 : -1;
			var enPassantSquare = board.EnPassantTargetSquare;
			var capturablePiecePosition = new BoardPosition(
				enPassantSquare.Position.Column, enPassantSquare.Position.Row - dir);

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

			if (pieceToCapture.Color == pieceToMove.Color)
				throw new InvalidOperationException("Trying to capture a Piece of the same color.");

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
		internal CastlingData(IChessPieceModel king, IChessPieceModel rook, CastleSide side, PlayerColor color)
		{
			KingState = new(king);
			RookState = new(rook);

			switch (side)
			{
				case CastleSide.King:
					RookOriginalPosition = new(color == PlayerColor.White ? "h1" : "h8");
					RookTargetPosition   = new(color == PlayerColor.White ? "f1" : "f8");
					break;
				case CastleSide.Queen:
					RookOriginalPosition = new(color == PlayerColor.White ? "a1" : "a8");
					RookTargetPosition   = new(color == PlayerColor.White ? "d1" : "d8");
					break;
				default:
					RookOriginalPosition = null;
					RookTargetPosition   = null;
					break;
			}
		}

		public PieceState RookState { get; }

		internal BoardPosition? RookOriginalPosition { get; }
		internal BoardPosition? RookTargetPosition   { get; }
		internal PieceState     KingState            { get; }
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
