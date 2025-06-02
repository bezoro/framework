using System;

namespace Bezoro.Core.Chess
{
	public class MovePieceCommand : IChessCommand
	{
		private const string DifferentSquaresMessage = "Source and destination squares must differ.";
		private const string FriendlyCaptureMessage = "Cannot move onto a square occupied by your own piece.";
		private const string PieceMismatchMessage = "The piece to move is not on the source square.";
		private const string RemoveCaptureFailureMessage = "Failed to remove the captured piece from its square.";
		private const string RemoveMoveFailureMessage = "Failed to remove the moving piece from its source square.";
		private const string SetMoveFailureMessage = "Failed to place the moving piece on its destination square.";

		// Extracted error messages
		private const string TargetOffBoardMessage = "Target square is off-board.";

		public MovePieceCommand(IChessPieceModel movingPiece, IChessBoardSquareModel destinationSquare)
		{
			PieceToMove = movingPiece ?? throw new ArgumentNullException(nameof(movingPiece));
			From = movingPiece.Square
				   ?? throw new ArgumentException(
					   "Piece must be on a board square.", nameof(movingPiece));

			To = destinationSquare ?? throw new ArgumentNullException(nameof(destinationSquare));
		}

		private IChessPieceModel?      _capturedPiece;
		public  IChessBoardSquareModel From { get; }
		public  IChessBoardSquareModel To   { get; }

		public IChessPieceModel PieceToMove { get; }

	#region Interface Implementations

		public void Execute(IChessBoardModel board)
		{
			Validate(board);

			var targetPiece = To.Piece;
			EnsureNotFriendlyCapture(targetPiece);

			if (targetPiece != null)
				HandleCapture(targetPiece, board);

			PerformMove(board);
		}

		public void Undo(IChessBoardModel board)
		{
			if (board == null)
				throw new ArgumentNullException(nameof(board));

			if (From == To)
				throw new InvalidOperationException(DifferentSquaresMessage);

			// 1) Remove the moved piece from the destination
			if (!To.TryRemovePiece(PieceToMove))
				throw new InvalidOperationException(RemoveMoveFailureMessage);

			// 2) Restore any captured piece
			if (_capturedPiece != null)
			{
				if (!To.TrySetPiece(_capturedPiece))
					throw new InvalidOperationException("Failed to restore the captured piece to its square.");

				_capturedPiece.IsCaptured = false;
				board.CapturedPieces.Remove(_capturedPiece);
				board.BoardPieces.Add(_capturedPiece);
			}

			// 3) Put the moving piece back
			if (!From.TrySetPiece(PieceToMove))
				throw new InvalidOperationException("Failed to return the moving piece to its source square.");
		}

		// These overloads are not supported; use the IChessBoardModel variants.
		public void Execute()
			=> throw new NotSupportedException("Use Execute(IChessBoardModel) instead.");

		public void Undo()
			=> throw new NotSupportedException("Use Undo(IChessBoardModel) instead.");

	#endregion

		private void EnsureNotFriendlyCapture(IChessPieceModel? piece)
		{
			if (piece?.Color == PieceToMove.Color)
				throw new InvalidOperationException(FriendlyCaptureMessage);
		}

		private void HandleCapture(IChessPieceModel captured, IChessBoardModel board)
		{
			if (!To.TryRemovePiece(captured))
				throw new InvalidOperationException(RemoveCaptureFailureMessage);

			_capturedPiece            = captured;
			_capturedPiece.IsCaptured = true;
			board.CapturedPieces.Add(_capturedPiece);
			board.BoardPieces.Remove(captured);
		}

		private void PerformMove(IChessBoardModel board)
		{
			if (!From.TryRemovePiece(PieceToMove))
				throw new InvalidOperationException(RemoveMoveFailureMessage);

			if (!To.TrySetPiece(PieceToMove))
				throw new InvalidOperationException(SetMoveFailureMessage);

			board.SetPieceAt(PieceToMove, To);
		}

		private void Validate(IChessBoardModel board)
		{
			if (board == null)
				throw new ArgumentNullException(nameof(board));

			var file = To.Position.File;
			var rank = To.Position.Rank;
			if (file < 0 || file >= board.Width || rank < 0 || rank >= board.Height)
				throw new InvalidOperationException(TargetOffBoardMessage);

			if (From == To)
				throw new InvalidOperationException(DifferentSquaresMessage);

			if (From.Piece != PieceToMove)
				throw new InvalidOperationException(PieceMismatchMessage);
		}
	}
}
