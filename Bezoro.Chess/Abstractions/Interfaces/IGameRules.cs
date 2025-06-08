using System.Collections.Generic;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;

namespace Bezoro.Chess.Abstractions.Interfaces
{
	public interface IGameRules
	{
		IEnumerable<Move> FilterLegalMoves(GameModel game, IEnumerable<Move> pseudoMoves);
	}
}
