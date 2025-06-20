using Bezoro.Chess.API.Shared.Enums;
using Bezoro.Chess.Domain.Extensions;
using Bezoro.Chess.Domain.Types.Structs;
using MoveType = Bezoro.Chess.API.Shared.Enums.MoveType;
using PieceType = Bezoro.Chess.API.Shared.Enums.PieceType;
using PromotionType = Bezoro.Chess.API.Shared.Enums.PromotionType;

namespace Bezoro.Chess.API.ViewModels
{
	public struct MoveViewModel
	{
		internal MoveViewModel(in Move move)
		{
			Type               = move.Type.ToAPI();
			CapturedPieceType  = move.CapturedPiece.Type.ToAPI();
			MovingPieceType    = move.Piece.Type.ToAPI();
			From               = move.From.Coordinate;
			To                 = move.To.Coordinate;
			PromotionPieceType = move.PromotionPieceType.ToAPI();
		}

		public ChessSquareCoordinate From { get; }
		public ChessSquareCoordinate To   { get; }

		public MoveType      Type               { get; }
		public PieceType     CapturedPieceType  { get; }
		public PieceType     MovingPieceType    { get; }
		public PromotionType PromotionPieceType { get; }
	}
}
