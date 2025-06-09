using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Game
{
	public record GameStateMemento(
		PlayerColor ActiveColor,
		CastlingRights CastlingRights,
		IChessBoardSquareModel EnPassantTargetSquare,
		int HalfMoveClock,
		int FullMoveNumber);
}
