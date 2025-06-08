using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Pieces.Models
{
	public class KnightModel : PieceModel
	{
		public KnightModel(PlayerColor color) : base(color, new KnightPseudoLegalMovesGenerator()) { }
	}
}
