using System;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Moves.Services;

namespace Bezoro.Chess.Pieces.Models
{
	public class PawnModel : PieceModel
	{
		public PawnModel(PlayerColor color)
			: base(color, new PawnPseudoValidMovesGenerator())
		{
			Direction = color == PlayerColor.White ? 1 : -1;
		}

		public int  Direction              { get; }
		public bool CanBeCapturedEnPassant { get; private set; }
		public bool JustAdvancedTwoSquares { get; private set; }

		public override void ResetMoved()
		{
			base.ResetMoved();
			CanBeCapturedEnPassant = false;
			JustAdvancedTwoSquares = false;
		}

		public void PromoteTo(IChessBoardSquareModel promotionSquare, PromotionPieceType newType)
		{
			PieceModel newPiece = newType switch
			{
				PromotionPieceType.Queen => new QueenModel(Color),
				PromotionPieceType.Rook => new RookModel(Color),
				PromotionPieceType.Bishop => new BishopModel(Color),
				PromotionPieceType.Knight => new KnightModel(Color),
				PromotionPieceType.None => throw new ArgumentException("Cannot promote to None", nameof(newType)),
				_ => throw new ArgumentException("Invalid promotion piece type", nameof(newType))
			};

			promotionSquare.SetPiece(newPiece);
		}

		public void SetEnPassantCapturable(bool value) => CanBeCapturedEnPassant = value;
		public void SetJustAdvancedTwoSquares(bool value) => JustAdvancedTwoSquares = value;
	}
}
