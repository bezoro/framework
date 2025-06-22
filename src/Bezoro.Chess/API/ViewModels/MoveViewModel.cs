using Bezoro.Chess.API.Shared.Enums;
using Bezoro.Chess.Domain.Extensions;
using Bezoro.Chess.Domain.Types.Structs;
using MoveType = Bezoro.Chess.API.Shared.Enums.MoveType;
using PromotionType = Bezoro.Chess.API.Shared.Enums.PromotionType;

namespace Bezoro.Chess.API.ViewModels
{
	public struct MoveViewModel
	{
		internal MoveViewModel(in Move move)
		{
			Type = move.Type.ToAPI();
			From = move.From.Coordinate;
			To   = move.To.Coordinate;
			move.TryAsPromotion(out Move.PromotionView promotionView);
			PromotionPieceType = promotionView.PromotionPieceType.ToAPI();
			Piece              = new PieceViewModel((move.Piece.Type, move.Piece.Color));
			CapturedPiece = new PieceViewModel(
				(move.AsCapture().Value.CapturedPiece.Type, move.AsCapture().Value.CapturedPiece.Color));
		}

		public ChessSquareCoordinate From               { get; }
		public ChessSquareCoordinate To                 { get; }
		public MoveType              Type               { get; }
		public PieceViewModel        CapturedPiece      { get; }
		public PieceViewModel        Piece              { get; }
		public PromotionType         PromotionPieceType { get; }
	}
}
