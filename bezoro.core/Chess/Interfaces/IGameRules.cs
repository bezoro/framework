using System.Collections.Generic;
using Bezoro.Core.Chess.Pieces;

namespace Bezoro.Core.Chess.Interfaces
{
	public interface IGameRules
	{
		IEnumerable<Move> FilterLegalMoves(IChessBoardModel board, IChessPieceModel piece);
	}
}
