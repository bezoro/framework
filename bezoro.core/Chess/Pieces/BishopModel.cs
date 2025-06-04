using System;
using System.Collections.Generic;

namespace Bezoro.Core.Chess.Utils
{
	public class BishopModel : PieceModel
	{
		public BishopModel(PlayerColor color) : base(color, new BishopPseudoValidMovesGenerator()) { }
	}

	public class BishopPseudoValidMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		public IEnumerable<Move> Generate(IChessBoardModel board, PieceModel piece) =>
			throw new NotImplementedException();

	#endregion
	}
}
