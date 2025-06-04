using System;
using System.Collections.Generic;
using Bezoro.Core.Chess.Interfaces;

namespace Bezoro.Core.Chess.Pieces
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
