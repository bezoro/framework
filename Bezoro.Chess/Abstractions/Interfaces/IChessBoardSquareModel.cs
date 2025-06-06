using Bezoro.Chess.Board;

namespace Bezoro.Chess.Abstractions.Interfaces
{
	public interface IChessBoardSquareModel
	{
		BoardPosition     Position   { get; }
		bool              IsEmpty    { get; }
		bool              IsOccupied { get; }
		IChessPieceModel? Piece      { get; }
		IChessPieceModel? GetPiece();
		void ClearPiece();
		void RemovePiece(IChessPieceModel piece);
		void SetPiece(IChessPieceModel? piece);
	}
}
