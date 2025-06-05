using Bezoro.Core.Chess.Abstractions.Interfaces;

namespace Bezoro.Core.Chess.Board.Models
{
	public class BoardSquareModel : IChessBoardSquareModel
	{
		public BoardSquareModel(BoardPosition position, IChessPieceModel? piece = null)
		{
			Position = position;
			Piece    = piece;
		}

		public BoardSquareModel(int col, int row) : this(new(col, row)) { }

		public BoardPosition     Position   { get; }
		public bool              IsEmpty    => Piece == null;
		public bool              IsOccupied => Piece != null;
		public IChessPieceModel? Piece      { get; private set; }

	#region Interface Implementations

		public void SetPiece(IChessPieceModel? piece) =>
			Piece = piece;

		public void RemovePiece(IChessPieceModel piece)
		{
			if (piece != Piece)
				return;

			Piece = null;
		}

		public void ClearPiece() => Piece = null;

		public IChessPieceModel? GetPiece() =>
			Piece;

	#endregion
	}
}
