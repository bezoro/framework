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

		public ChessPieceType Type { get; }

		public PlayerColor             Color      { get; }
		public bool                    IsCaptured { get; set; }
		public bool                    IsSelected { get; set; }
		public ChessPosition           Position   { get; set; }
		public IChessBoardSquareModel? Square     { get; set; }

		public event Action? CapturedEnemyPiece;
		public event Action? WasCaptured;
		public event Action? WasMoved;
		public event Action? WasSelected;
		public event Action? WasUnselected;

	#region Interface Implementations

		public bool TryRemoveSelfFromBoard(IChessBoardModel board)
		{
			if (board == null)
				throw new ArgumentNullException(nameof(board));

			if (Square == null)
				throw new InvalidOperationException("Piece must be on a board square.");

			if (IsCaptured)
				throw new InvalidOperationException("Cannot remove a captured piece from the board.");

			if (!Square.TryRemovePiece(this))
				throw new InvalidOperationException($"Piece {this} was not found on square {Square}.");

			if (!board.BoardPieces.Remove(this))
				throw new InvalidOperationException($"Piece {this} was not found on board.");

			Square = null;
			return true;
		}

		public bool TryGetCaptured(IChessBoardModel board)
		{
			TryRemoveSelfFromBoard(board);

			IsCaptured = true;
			if (!board.CapturedPieces.Contains(this))
				board.CapturedPieces.Add(this);

			WasCaptured?.Invoke();
			return true;
		}

	#endregion

		public bool TryGetSelected()
		{
			if (IsSelected)
				return false;
			
			IsSelected = true;
			WasSelected?.Invoke();
			return true;
		}

		public bool TryMove(IChessBoardSquareModel to)
		{
            if (to == null)
                throw new ArgumentNullException(nameof(to));
            if (Square == null)
                throw new InvalidOperationException("Piece must be on a board square.");
            if (IsCaptured)
                throw new InvalidOperationException("Cannot move a captured piece.");
            if (Square.Piece != this)
                throw new InvalidOperationException("Piece must be on its current square.");

            // 1) Handle capture of an occupying piece
            if (to.Piece is ChessPieceModel occupant && occupant.Color != this.Color)
            {
                // occupant.TryGetCaptured(Square.Board);  // assumes square knows its board
                CapturedEnemyPiece?.Invoke();
            }
            else if (to.Piece != null)
            {
                throw new InvalidOperationException($"Cannot move onto occupied square {to}.");
            }

            // 2) Transfer piece
            if (!Square.TryRemovePiece(this))
                throw new InvalidOperationException("Failed to remove piece from original square.");

            Square = to;

            if (!Square.TrySetPiece(this))
                throw new InvalidOperationException("Failed to place piece on destination square.");

            // 3) Update position
            Position = to.Position;

            // 4) Fire moved event
            WasMoved?.Invoke();
            return true;
		}

		public bool TryDeselect()
		{
            if (!IsSelected) 
				return false;
            
			IsSelected = false;
            WasUnselected?.Invoke();
            return true;
		}
	}
}