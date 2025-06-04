using System;
using System.Collections.Generic;

namespace Bezoro.Core.Chess.Utils
{
	public class QueenModel : PieceModel
	{
		public QueenModel(PlayerColor color) : base(color, new QueenPseudoValidMovesGenerator()) { }
	}

	public class QueenPseudoValidMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		public IEnumerable<Move> Generate(IChessBoardModel board, PieceModel piece) =>
			throw new NotImplementedException();

	#endregion
	}
}
