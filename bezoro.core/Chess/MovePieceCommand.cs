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
			throw new NotImplementedException();
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

		public void Undo(IChessBoardModel board) =>
			throw new NotImplementedException();

	#endregion

		private void EnsureNotFriendlyCapture(IChessPieceModel? piece)
		{
			if (piece?.Color == PieceToMove.Color)
				throw new InvalidOperationException(FriendlyCaptureMessage);
		}

		private void HandleCapture(IChessPieceModel captured, IChessBoardModel board) =>
			throw new NotImplementedException();

		private void PerformMove(IChessBoardModel board) { }

		private void Validate(IChessBoardModel board)
		{
			if (board == null)
				throw new ArgumentNullException(nameof(board));

			var file = To.Position.Column;
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
