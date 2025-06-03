using System;
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
		bool TryMovePiece(MovePieceCommand movePieceCommand);
		void SetPieceAt(IChessPieceModel pieceToMove, IChessBoardSquareModel to);
	}

	public interface IChessBoardSquareModel
	{
		BoardPosition     Position                 { get; }
		bool              IsEmpty                  { get; }
		bool              IsHighlightedAsValidMove { get; set; }
		bool              IsOccupied               { get; }
		bool              IsSelected               { get; set; }
		IChessPieceModel? Piece                    { get; set; }

		bool TryRemovePiece(IChessPieceModel pieceToRemove);
		bool TrySetPiece(IChessPieceModel pieceToSet);
	}

	public interface IChessCommand
	{
		IChessBoardSquareModel From        { get; }
		IChessBoardSquareModel To          { get; }
		IChessPieceModel       PieceToMove { get; }

		void Execute(IChessBoardModel board);
		void Undo(IChessBoardModel board);
	}

	public interface IChessPieceModel
	{
		BoardPosition           Position   { get; set; }
		bool                    IsCaptured { get; set; }
		bool                    IsSelected { get; set; }
		ChessPieceType          Type       { get; }
		IChessBoardSquareModel? Square     { get; set; }
		PlayerColor             Color      { get; }
		bool TryDeselect();
		bool TryGetCaptured(IChessBoardModel board);
		bool TryGetSelected();
		bool TryMove(IChessBoardModel board, IChessBoardSquareModel to);
		bool TryRemoveSelfFromBoard(IChessBoardModel board);
		event Action? CapturedEnemyPiece;
		event Action? WasCaptured;
		event Action? WasMoved;
		event Action? WasSelected;
		event Action? WasUnselected;
	}
}
