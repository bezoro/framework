using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Pieces.Models
{
	public class BishopModel : PieceModel
	{
		public BishopModel(PlayerColor color) : base(color, new BishopPseudoLegalMovesGenerator()) { }
	}
}
