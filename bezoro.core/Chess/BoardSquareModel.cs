using System;
using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess
{
	public class BoardSquareModel : IChessBoardSquareModel
	{
		public BoardSquareModel(BoardPosition position)
		{
			Position = position;
			Piece    = null; // Start with no piece on the square
		}

		public BoardSquareModel(int row, int col) : this(new(row, col)) { }

		public BoardPosition Position   { get; }
		public bool          IsEmpty    => Piece == null;
		public bool          IsOccupied => Piece != null;

		public bool              IsHighlightedAsValidMove { get; set; }
		public bool              IsSelected               { get; set; }
		public IChessPieceModel? Piece                    { get; set; }

	#region Interface Implementations

		public bool TryRemovePiece(IChessPieceModel pieceToRemove)
		{
			if (Piece != pieceToRemove)
				return false;

			pieceToRemove.Square   = null;
			pieceToRemove.Position = default;
			Piece                  = null;
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
