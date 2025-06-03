using System.Collections.Generic;

namespace Bezoro.Core.Chess
{
	public interface IChessBoardModel
	{
		CastlingRights            CastlingRights { get; }
		IChessBoardSquareModel[,] Squares        { get; }
		int                       Height         { get; }
		int                       Width          { get; }
		List<IChessPieceModel>    BoardPieces    { get; }
		List<IChessPieceModel>    CapturedPieces { get; set; }
		BoardPosition? GetPosition(IChessPieceModel piece);
		bool TryMovePiece(MovePieceCommand movePieceCommand);
		void SetPieceAt(IChessPieceModel pieceToMove, IChessBoardSquareModel to);
	}

	public interface IChessBoardSquareModel
	{
		BoardPosition     Position   { get; }
		bool              IsEmpty    { get; }
		bool              IsOccupied { get; }
		IChessPieceModel? Piece      { get; }
		IChessPieceModel? GetPiece();
		void ClearPiece();
		void RemovePiece(IChessPieceModel piece);
		void SetPiece(IChessPieceModel piece);
	}

	public interface IChessPieceModel
	{
		bool        HasMoved { get; }
		PlayerColor Color    { get; }

		IEnumerable<Move> GetValidMoves(IChessBoardModel board);
		void MarkMoved();
		void ResetMoved();
	}
}
