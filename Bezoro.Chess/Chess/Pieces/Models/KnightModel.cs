using System;
using System.Collections.Generic;
using Bezoro.Chess.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Chess.Common.Enums;
using Bezoro.Chess.Chess.Game.Models;
using Bezoro.Chess.Chess.Moves.Models;

namespace Bezoro.Chess.Chess.Pieces.Models
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
