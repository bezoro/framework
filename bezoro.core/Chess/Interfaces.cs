using System.Collections.Generic;

namespace Bezoro.Core.Chess
{
	public interface IChessBoardModel
	{
		IChessBoardSquareModel[,] Squares        { get; }
		int                       Height         { get; }
		int                       Width          { get; }
		List<IChessPieceModel>    BoardPieces    { get; }
		List<IChessPieceModel>    CapturedPieces { get; set; }
		bool TryMovePiece(IChessPieceModel pieceToMove, MovePieceCommand movePieceCommand);
	}

	public interface IChessBoardSquareModel
	{
		bool              IsEmpty                  { get; }
		bool              IsHighlightedAsValidMove { get; set; }
		bool              IsOccupied               { get; }
		bool              IsSelected               { get; set; }
		ChessPosition     Position                 { get; }
		IChessPieceModel? Piece                    { get; set; }
		
		bool TryRemovePiece(IChessPieceModel pieceToRemove);
	}

	public interface ICommand
	{
		IChessBoardSquareModel From  { get; }
		IChessBoardSquareModel To    { get; }
		IChessPieceModel       Piece { get; }
		void Execute();
		void Undo();
	}

	public interface IChessPieceModel
	{
		bool                    IsCaptured { get; set; }
		bool                    IsSelected { get; set; }
		ChessPieceType          Type       { get; }
		ChessPosition           Position   { get; set; }
		IChessBoardSquareModel? Square     { get; set; }
		PlayerColor             Color      { get; }
	}
}
