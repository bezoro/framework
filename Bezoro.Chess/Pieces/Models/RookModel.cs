using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Pieces.Models
{
	/// <summary>
	///     Runtime representation of a rook on the chess board.
	/// </summary>
	public sealed class RookModel : PieceModel
	{
		public RookModel(PlayerColor color)
			: base(color, new RookPseudoLegalMovesGenerator()) { }
	}
}
