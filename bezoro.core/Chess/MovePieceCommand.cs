using System;

namespace Bezoro.Core.Chess
{
	public class MovePieceCommand : IChessCommand
	{
		public MovePieceCommand(IChessPieceModel movingPiece, IChessBoardSquareModel destinationSquare)
		{
			PieceToMove = movingPiece ?? throw new ArgumentNullException(nameof(movingPiece));
			From = movingPiece.Square
				   ?? throw new ArgumentException(
					   "Piece must be on a board square.", nameof(movingPiece));

			To = destinationSquare ?? throw new ArgumentNullException(nameof(destinationSquare));
		}

		private IChessPieceModel? _capturedPiece;

		public IChessPieceModel       PieceToMove { get; }
		public IChessBoardSquareModel From        { get; }
		public IChessBoardSquareModel To          { get; }

	#region Interface Implementations

		public void Execute(IChessBoardModel board)
		{
			if (board == null)
				throw new ArgumentNullException(nameof(board));

			if (From == To)
				throw new InvalidOperationException("Source and destination squares must differ.");

			if (From.Piece != PieceToMove)
				throw new InvalidOperationException("The piece to move is not on the source square.");

			// Handle capture of an enemy piece
			if (To.Piece != null)
			{
				var targetPiece = To.Piece;
				if (targetPiece.Color == PieceToMove.Color)
					throw new InvalidOperationException("Cannot move onto a square occupied by your own piece.");

				// remove captured piece from its square
				if (!To.TryRemovePiece(targetPiece))
					throw new InvalidOperationException("Failed to remove the captured piece from its square.");

				_capturedPiece            = targetPiece;
				_capturedPiece.IsCaptured = true;
				board.CapturedPieces.Add(_capturedPiece);
				board.BoardPieces.Remove(targetPiece);
			}
			else
			{
				_capturedPiece = null;
			}

			// Move the piece
			if (!From.TryRemovePiece(PieceToMove))
				throw new InvalidOperationException("Failed to remove the moving piece from its source square.");

			if (!To.TrySetPiece(PieceToMove))
				throw new InvalidOperationException("Failed to place the moving piece on its destination square.");

			board.SetPieceAt(PieceToMove, To);
		}

		public void Undo(IChessBoardModel board)
		{
			if (board == null) throw new ArgumentNullException(nameof(board));
			if (From == To)
				throw new InvalidOperationException("Source and destination squares must differ.");

			// Remove the moved piece from the destination
			if (!To.TryRemovePiece(PieceToMove))
				throw new InvalidOperationException("Failed to remove the moving piece from its destination square.");

			// Restore any captured piece
			if (_capturedPiece != null)
			{
				if (!To.TrySetPiece(_capturedPiece))
					throw new InvalidOperationException("Failed to restore the captured piece to its square.");

				_capturedPiece.IsCaptured = false;
				board.CapturedPieces.Remove(_capturedPiece);
			}

			// Put the moving piece back
			if (!From.TrySetPiece(PieceToMove))
				throw new InvalidOperationException("Failed to return the moving piece to its source square.");
		}

		// These overloads are not supported; use the IChessBoardModel variants.
		public void Execute()
			=> throw new NotSupportedException("Use Execute(IChessBoardModel) instead.");

		public void Undo()
			=> throw new NotSupportedException("Use Undo(IChessBoardModel) instead.");

	#endregion
	}
}
