using System;
using System.Collections.Generic;
using Bezoro.Core.Chess.Abstractions.Interfaces;
using Bezoro.Core.Chess.Common.Enums;
using Bezoro.Core.Chess.Game.Models;
using Bezoro.Core.Chess.Moves.Models;

namespace Bezoro.Core.Chess.Pieces.Models
{
	public class QueenModel : PieceModel
	{
		public QueenModel(PlayerColor color) : base(color, new QueenPseudoValidMovesGenerator()) { }
	}

	public class QueenPseudoValidMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		public IEnumerable<Move> Generate(GameModel game, IChessPieceModel piece) =>
			throw new NotImplementedException();

	#endregion
	}
}
