using System.Collections.Generic;
using Bezoro.Chess.Chess.Game.Models;
using Bezoro.Chess.Chess.Moves.Models;

namespace Bezoro.Chess.Chess.Abstractions.Interfaces
{
	public interface IGameRules
	{
		IEnumerable<Move> FilterLegalMoves(GameModel game, IChessPieceModel piece, IEnumerable<Move> pseudoMoves);
	}
}
