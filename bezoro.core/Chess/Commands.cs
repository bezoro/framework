using System;
using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess
{
	public struct MovePieceCommand : ICommand
	{
		public MovePieceCommand(IChessPieceModel pieceToMove, IChessBoardSquareModel to)
		{
			Piece = pieceToMove ?? throw new ArgumentNullException(nameof(pieceToMove));

			From = pieceToMove.Square
				   ?? throw new ArgumentException(
					   "Piece must be on a board square.", nameof(pieceToMove));

			To = to ?? throw new ArgumentNullException(nameof(to));
		}

		public IChessBoardSquareModel From  { get; }
		public IChessBoardSquareModel To    { get; }
		public IChessPieceModel       Piece { get; }

	#region Interface Implementations

		public void Execute()
		{
			Piece.MoveTo(To);
		}

	#endregion

		public void Undo()
		{
			Piece.MoveTo(From);
		}
	}
}
