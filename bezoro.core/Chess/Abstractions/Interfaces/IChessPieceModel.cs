using System.Collections.Generic;
using Bezoro.Core.Chess.Pieces;

namespace Bezoro.Core.Chess.Interfaces
{
	public interface IChessPieceModel
	{
		bool        HasMoved { get; }
		PlayerColor Color    { get; }
		PlayerColor Opposite { get; }
		ChessPieceType GetPieceType();

		IEnumerable<Move> GetPseudoLegalMoves(GameModel game);
		void MarkMoved();
		void ResetMoved();
	}
}
