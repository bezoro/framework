namespace Bezoro.Chess.Abstractions.Interfaces
{
	public interface IChessCommand
	{
		IChessPieceModel MovingPiece { get; }

		void Execute(IChessBoardModel board);
		void Undo(IChessBoardModel board);
	}
}
