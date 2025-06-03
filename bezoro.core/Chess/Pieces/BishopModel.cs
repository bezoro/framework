using System;
using System.Collections.Generic;

namespace Bezoro.Core.Chess.Utils
{
	public class BishopModel : IChessPieceModel
	{
		public BishopModel(PlayerColor color)
		{
			throw new NotImplementedException();
		}

		public bool        HasMoved { get; }
		public PlayerColor Color    { get; }

	#region Interface Implementations

		public IEnumerable<Move> GetPseudoLegalMoves(IChessBoardModel board) =>
			throw new NotImplementedException();

		public void MarkMoved() =>
			throw new NotImplementedException();

		public void ResetMoved() =>
			throw new NotImplementedException();

	#endregion
	}
}
