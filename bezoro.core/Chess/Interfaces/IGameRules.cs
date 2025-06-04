using System.Collections.Generic;
using Bezoro.Core.Chess.Pieces;

namespace Bezoro.Core.Chess.Interfaces
{
	public interface IGameRules
	{
		IEnumerable<Move> FilterLegalMoves(GameModel board, IChessPieceModel piece);
	}
}
