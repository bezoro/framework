using System;
using System.Collections.Generic;

namespace Bezoro.Core.Chess.Pieces
{
	public class RookModel : PieceModel
	{
		public RookModel(PlayerColor color) : base(color, new RookPseudoValidMovesGenerator()) { }
	}

	public class RookPseudoValidMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		public IEnumerable<Move> Generate(IChessBoardModel board, PieceModel piece) =>
			throw new NotImplementedException();

	#endregion
	}
}
