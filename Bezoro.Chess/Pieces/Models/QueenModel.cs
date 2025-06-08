using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Pieces.Models
{
	public class QueenModel : PieceModel
	{
		public QueenModel(PlayerColor color) : base(color, new QueenPseudoLegalMovesGenerator()) { }
	}
}
