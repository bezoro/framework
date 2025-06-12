using Bezoro.Chess.Abstractions.Interfaces;

namespace Bezoro.Chess.Board.Models
{
	public class BoardSquareModel : IChessBoardSquareModel
	{
		public BoardSquareModel(BoardPosition position, IChessPieceModel? piece = null) : this(
			position.Column, position.Row, piece) { }

		public BoardSquareModel(uint col, uint row, IChessPieceModel? piece = null)
		{
			Position = new(col, row);
			Piece    = piece;
		}

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
