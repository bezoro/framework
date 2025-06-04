namespace Bezoro.Core.Chess
{
	public class KingModel : PieceModel
	{
		public KingModel(PlayerColor color, IChessBoardSquareModel? square = null)
			: base(color, new KingPseudoMoveGenerator()) { }

		public bool HasCastled   { get; private set; }
		public bool IsCheckMated { get; private set; }
		public bool IsInCheck    { get; private set; }

		public override void ResetMoved()
		{
			base.ResetMoved();
			HasCastled   = false;
			IsCheckMated = false;
			IsInCheck    = false;
		}

		public bool CanCastle(CastlingRights castlingRights, CastleSide side)
		{
			// 1. A king that has already moved or already castled can never castle again.
			if (HasMoved || HasCastled)
				return false;

			// 2. Map (color, side)
			var expectedFlag = Color switch
			{
				PlayerColor.White when side == CastleSide.KingSide => CastlingRights.WhiteKingSide,
				PlayerColor.White                                  => CastlingRights.WhiteQueenSide,
				PlayerColor.Black when side == CastleSide.KingSide => CastlingRights.BlackKingSide,
				_                                                  => CastlingRights.BlackQueenSide
			};

			// 3. The king can castle if that specific right is still available.
			return castlingRights.HasFlag(expectedFlag);
		}

		public void MarkCastled() => HasCastled = true;

		public void MarkCheckmated() => IsCheckMated = true;

		public void SetCheckState(bool inCheck) => IsInCheck = inCheck;
	}
}
