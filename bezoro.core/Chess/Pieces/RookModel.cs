using System;
using System.Collections.Generic;

namespace Bezoro.Core.Chess.Utils
{
	public class RookModel : IChessPieceModel
	{
		public RookModel(PlayerColor color)
		{
			throw new NotImplementedException();
		}

		public bool        HasMoved { get; }
		public PlayerColor Color    { get; }

	#region Interface Implementations

		public IEnumerable<Move> GetValidMoves(IChessBoardModel board) =>
			throw new NotImplementedException();

		public void MarkMoved() =>
			throw new NotImplementedException();

		public void ResetMoved() =>
			throw new NotImplementedException();

	#endregion
	}
}
