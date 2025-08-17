using Bezoro.Chess.API.Shared.Enums;
using Bezoro.Chess.Domain.Extensions;
using Bezoro.Chess.Domain.Types.Records;

namespace Bezoro.Chess.API.ViewModels
{
	public readonly struct GameStateViewModel
	{
		internal GameStateViewModel(GameState gameState)
		{
			Board           = gameState.Board.ToViewModel();
			CastlingRights  = gameState.Castling.ToAPI();
			EnPassantSquare = gameState.EnPassantTargetSquare.Coordinate;
			ActiveColor     = gameState.ActiveColor.ToAPI();
			FullmoveNumer   = gameState.FullMoveNumber;
			HalfmoveClock   = gameState.HalfMoveClock;
		}

		public BoardViewModel        Board           { get; }
		public CastlingRights        CastlingRights  { get; }
		public ChessSquareCoordinate EnPassantSquare { get; }
		public PieceColor            ActiveColor     { get; }
		public uint                  FullmoveNumer   { get; }
		public uint                  HalfmoveClock   { get; }
	}
}
