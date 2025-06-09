using System.Collections.Generic;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;

namespace Bezoro.Chess.Abstractions.Interfaces
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
		void ToggleMoved();
	}
}
