namespace Bezoro.Chess.Abstractions.Interfaces
{
	public interface IChessCommand
	{
		IChessBoardSquareModel From        { get; }
		IChessBoardSquareModel To          { get; }
		IChessPieceModel       PieceToMove { get; }

		void Execute(IChessBoardModel board);
		void Undo(IChessBoardModel board);
	}
}
