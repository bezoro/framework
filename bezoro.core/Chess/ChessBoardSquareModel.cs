using System;
using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess
{
	public class ChessBoardSquareModel : IChessBoardSquareModel
	{
		public ChessBoardSquareModel(ChessPosition position)
		{
			Position = position;
			Piece    = null; // Start with no piece on the square
		}

		public ChessBoardSquareModel(int row, int col) : this(new(row, col)) { }
	
		public ChessPosition Position { get; }
		public bool              IsEmpty    => Piece == null;
		public bool              IsOccupied => Piece != null;
		public IChessPieceModel? Piece      { get; set; }

		public bool IsHighlightedAsValidMove { get; set; }
		public bool IsSelected               { get; set; }

	#region Interface Implementations

		public bool TryRemovePiece(IChessPieceModel pieceToRemove)
		{
			if (Piece != pieceToRemove)
				return false;

			pieceToRemove.Square = null;
			pieceToRemove.Position = default;
			Piece                = null;
			return true;
		}

		public bool TrySetPiece(IChessPieceModel pieceToSet)
		{
			if (pieceToSet == null)
				throw new ArgumentNullException(nameof(pieceToSet));

			if (Piece == pieceToSet)
				return false;

			// ???: Should I allow it so we can overwrite a piece on the square?
			if (Piece != null)
				return false;

			pieceToSet.SetAtSquare(this);
			return true;
		}

	#endregion
	}
}
