using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Game
{
	public class GameStateMemento
	{
		public GameStateMemento(
			PlayerColor activeColor,
			CastlingRights castlingRights,
			IChessBoardSquareModel enPassantTargetSquare,
			int halfMoveClock,
			int fullMoveNumber)
		{
			ActiveColor           = activeColor;
			CastlingRights        = castlingRights;
			EnPassantTargetSquare = enPassantTargetSquare;
			HalfMoveClock         = halfMoveClock;
			FullMoveNumber        = fullMoveNumber;
		}

		public CastlingRights CastlingRights { get; }

		public IChessBoardSquareModel EnPassantTargetSquare { get; }
		public int                    FullMoveNumber        { get; }
		public int                    HalfMoveClock         { get; }
		public PlayerColor            ActiveColor           { get; }
	}
}
