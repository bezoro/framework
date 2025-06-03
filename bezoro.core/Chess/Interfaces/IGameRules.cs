using System.Collections.Generic;
using Bezoro.Core.Chess;

public interface IGameRules
{
	IEnumerable<Move> FilterLegalMoves(IChessBoardModel board, IChessPieceModel piece);
}
