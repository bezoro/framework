using System;
using System.Collections.Generic;
using Bezoro.Core.Chess.Interfaces;

namespace Bezoro.Core.Chess.Pieces
{
	public class KnightModel : PieceModel
	{
		public KnightModel(PlayerColor color) : base(color, new KnightPseudoValidMovesGenerator()) { }
	}

	public class KnightPseudoValidMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		public IEnumerable<Move> Generate(IChessBoardModel board, PieceModel piece) =>
			throw new NotImplementedException();

	#endregion
	}
}
