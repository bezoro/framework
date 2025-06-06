using System;
using System.Collections.Generic;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;

namespace Bezoro.Chess.Pieces.Models
{
	public class KnightModel : PieceModel
	{
		public KnightModel(PlayerColor color) : base(color, new KnightPseudoValidMovesGenerator()) { }
	}

	public class KnightPseudoValidMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		public IEnumerable<Move> Generate(GameModel game, IChessPieceModel piece) =>
			throw new NotImplementedException();

	#endregion
	}
}
