using System;
using Bezoro.Core.Chess.Interfaces;
using Bezoro.Core.Chess.Pieces;

namespace Bezoro.Core.Chess
{
	public sealed class MovePieceCommand : IChessCommand
	{
	#region Ctor

		/// <summary>
		///     Creates a command that moves the piece described by <paramref name="move" />.
		///     The board instance is needed only for looking up the squares – it is not
		///     modified in the constructor.
		/// </summary>
		public MovePieceCommand(Move move, IChessBoardModel board)
		{
			if (board is null)
				throw new ArgumentNullException(nameof(board));

			_move            = move;
			_isPromotion     = move.IsPromotion;
			_promotionTarget = move.PromoteTo;

			From = board.GetSquare(move.From)
				   ?? throw new InvalidOperationException("Source square not found on board.");

			To = board.GetSquare(move.To)
				 ?? throw new InvalidOperationException("Target square not found on board.");

			PieceToMove = From.Piece
						  ?? throw new InvalidOperationException("There is no piece on the source square.");

			if (_isPromotion && PieceToMove is not PawnModel)
				throw new InvalidOperationException("Only pawns can be promoted.");
		}

	#endregion

	#region Error-message constants

		private const string DifferentSquaresMessage = "Source and destination squares must differ.";
		private const string FriendlyCaptureMessage = "Cannot move onto a square occupied by your own piece.";
		private const string PieceMismatchMessage = "The piece to move is not on the source square.";
		private const string RemoveCaptureFailureMessage = "Failed to remove the captured piece from its square.";
		private const string RemoveMoveFailureMessage = "Failed to remove the moving piece from its source square.";
		private const string SetMoveFailureMessage = "Failed to place the moving piece on its destination square.";
		private const string TargetOffBoardMessage = "Target square is off-board.";

	#endregion

	#region Private state

		private readonly Move                _move; // 🆕 value-type ‘Move’
		private readonly bool                _isPromotion;
		private readonly PromotionPieceType? _promotionTarget;

		private IChessPieceModel? _capturedPiece;
		private IChessPieceModel? _createdPromotedPiece;

	#endregion

	#region Public surface

		public IChessBoardSquareModel From        { get; }
		public IChessBoardSquareModel To          { get; }
		public IChessPieceModel       PieceToMove { get; }

	#endregion

	#region IChessCommand implementation

		public void Execute(IChessBoardModel board)
		{
			Validate(board);

			_capturedPiece = To.Piece;
			EnsureNotFriendlyCapture(_capturedPiece);

			if (_capturedPiece is not null)
				HandleCapture(_capturedPiece, board);

			if (_isPromotion)
				PerformPromotion(board);
			else
				PerformMove(board);
		}

		public void Undo(IChessBoardModel board)
		{
			if (_isPromotion)
			{
				// remove promoted piece, restore captured (if any) and pawn
				To.SetPiece(_capturedPiece);
				From.SetPiece(PieceToMove);
				_createdPromotedPiece = null;
			}
			else
			{
				// normal rollback
				From.SetPiece(PieceToMove);
				To.SetPiece(_capturedPiece);
			}
		}

	#endregion

	#region Private helpers

		private void EnsureNotFriendlyCapture(IChessPieceModel? piece)
		{
			if (piece?.Color == PieceToMove.Color)
				throw new InvalidOperationException(FriendlyCaptureMessage);
		}

		private void HandleCapture(IChessPieceModel captured, IChessBoardModel board)
		{
			// Basic capture handling – remove the piece from its square.
			if (To.Piece != captured)
				throw new InvalidOperationException(RemoveCaptureFailureMessage);

			To.SetPiece(null);
		}

		private void PerformMove(IChessBoardModel board)
		{
			if (From.Piece != PieceToMove)
				throw new InvalidOperationException(RemoveMoveFailureMessage);

			From.SetPiece(null);
			To.SetPiece(PieceToMove);
		}

		private void PerformPromotion(IChessBoardModel board)
		{
			// Remove pawn from its source square
			From.SetPiece(null);

			_createdPromotedPiece = _promotionTarget switch
			{
				PromotionPieceType.Queen  => new QueenModel(PieceToMove.Color),
				PromotionPieceType.Rook   => new RookModel(PieceToMove.Color),
				PromotionPieceType.Bishop => new BishopModel(PieceToMove.Color),
				PromotionPieceType.Knight => new KnightModel(PieceToMove.Color),
				_                         => throw new InvalidOperationException("Unsupported promotion type.")
			};

			To.SetPiece(_createdPromotedPiece);
		}

		private void Validate(IChessBoardModel board)
		{
			if (board == null)
				throw new ArgumentNullException(nameof(board));

			// off-board check
			var file = To.Position.Column;
			var rank = To.Position.Rank;
			if (file < 0 || file >= board.Width || rank < 0 || rank >= board.Height)
				throw new InvalidOperationException(TargetOffBoardMessage);

			// must move to a different square
			if (From == To)
				throw new InvalidOperationException(DifferentSquaresMessage);

			// piece-square consistency
			if (From.Piece != PieceToMove)
				throw new InvalidOperationException(PieceMismatchMessage);
		}

	#endregion
	}
}
