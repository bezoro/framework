using Bezoro.Chess.Game.Models;

namespace Bezoro.Chess.Abstractions.Interfaces
{
	public interface IChessCommand
	{
		void Execute(GameModel game);
		void Undo(GameModel game);
	}
}
