using System;

namespace Bezoro.Core.Chess
{
	public class ChessPieceModel : IChessPieceModel
	{
		public ChessPieceModel(PlayerColor color, ChessPieceType type)
		{
			Color = color;
			Type  = type;
		}

		public PlayerColor             Color      { get; }
		public ChessPieceType          Type       { get; }
		public IChessBoardSquareModel? Square     { get; set; }
		public ChessPosition           Position   { get; set; }
		public bool                    IsCaptured { get; set; }
		public bool                    IsSelected { get; set; }

		public event Action? CapturedEnemyPiece;
		public event Action? WasCaptured;
		public event Action? WasMoved;
		public event Action? WasSelected;

	#region Interface Implementations

		public bool TryRemoveSelfFromBoard(IChessBoardModel board)
		{
			if (board == null)
				throw new ArgumentNullException(nameof(board));

			if (Square == null)
				throw new InvalidOperationException("Piece must be on a board square.");

			if (IsCaptured)
				throw new InvalidOperationException("Cannot remove a captured piece from the board.");

			board.BoardPieces.Remove(this);

			if (!Square.TryRemovePiece(this))
				throw new InvalidOperationException($" piece {this} was not found on square {Square}.");

			Square = null;
			return true;
		}

		public bool TryGetCaptured(IChessBoardModel board)
		{
			try { TryRemoveSelfFromBoard(board); }
			catch (InvalidOperationException e)
			{
				Console.WriteLine(e);
				throw;
			}

			IsCaptured = true;
			board.CapturedPieces.Add(this);
			WasCaptured?.Invoke();
			return true;
		}

	#endregion

		public bool TryGetSelected()
		{
			IsSelected = true;
			WasSelected?.Invoke();
			return true;
		}

		public bool TryMove(IChessBoardSquareModel to)
		{
			if (Square == null)
				throw new InvalidOperationException("Piece must be on a board square.");

			if (IsCaptured)
				throw new InvalidOperationException("Cannot move a captured piece.");

			if (Square.Piece != this)
				throw new InvalidOperationException("Piece must be on the board.");

			Square.TryRemovePiece(this);
			Square = to;
			Square.TrySetPiece(this);
			return true;
		}
	}
}
