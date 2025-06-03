using System;

namespace Bezoro.Core.Chess
{
	public class PawnModel : PieceModel
	{
		public PawnModel(PlayerColor color)
			: base(color, new PawnPseudoMoveGenerator())
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

		public void PromoteTo(PromotionPieceType newType) =>
			throw new NotImplementedException();

		public void SetEnPassantCapturable(bool value) => CanBeCapturedEnPassant = value;
		public void SetJustAdvancedTwoSquares(bool value) => JustAdvancedTwoSquares = value;
	}
}
