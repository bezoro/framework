using System.Collections.Generic;
using Bezoro.Core.Chess.Common.Enums;
using Bezoro.Core.Chess.Game.Models;
using Bezoro.Core.Chess.Moves.Models;

namespace Bezoro.Core.Chess.Abstractions.Interfaces
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
