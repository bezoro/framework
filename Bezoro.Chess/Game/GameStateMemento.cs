using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Game
{
	public class GameStateMemento
	{
		public GameStateMemento(
			PlayerColor activeColor,
			CastlingRights castlingRights,
			BoardPosition? enPassantTargetSquare,
			int halfMoveClock,
			int fullMoveNumber)
		{
			ActiveColor           = activeColor;
			CastlingRights        = castlingRights;
			EnPassantTargetSquare = enPassantTargetSquare;
			HalfMoveClock         = halfMoveClock;
			FullMoveNumber        = fullMoveNumber;
		}

		public BoardPosition? EnPassantTargetSquare { get; }
		public CastlingRights CastlingRights { get; }
		public int            FullMoveNumber { get; }
		public int            HalfMoveClock  { get; }
		public PlayerColor    ActiveColor    { get; }
	}
}
