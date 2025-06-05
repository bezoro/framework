using System.Collections.Generic;
using Bezoro.Core.Chess.Game.Models;
using Bezoro.Core.Chess.Moves.Models;

namespace Bezoro.Core.Chess.Abstractions.Interfaces
{
	public interface IGameRules
	{
		IEnumerable<Move> FilterLegalMoves(GameModel game, IChessPieceModel piece, IEnumerable<Move> pseudoMoves);
	}
}
