using System.Collections.Generic;
using Bezoro.Core.Chess.Board;
using Bezoro.Core.Chess.Common.Enums;
using Bezoro.Core.Chess.Game.Models;
using Bezoro.Core.Chess.Moves.Models;
using Bezoro.Core.Chess.Rules;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Chess
{
	// TODO: Finish tests
	[TestFixture]
	public sealed class StandardChessRules_PawnB2_Tests
	{
	#region Test Methods

		[Test]
		public void FilterLegalMoves_PawnAtB2_ReturnsLegalForwardMoves()
		{
			var game     = new GameModel();
			var pawnB2   = game.Board.GetPieceAt("b2");
			var squareB2 = game.Board.GetSquare("b2");

			Assert.Multiple(
				() =>
				{
					Assert.That(pawnB2,                Is.Not.Null);
					Assert.That(pawnB2.GetPieceType(), Is.EqualTo(ChessPieceType.Pawn));
					Assert.That(pawnB2.Color,          Is.EqualTo(PlayerColor.White));
				});

			var rules = new StandardChessRules();

			// --- Build moves in the engine’s internal coordinate system ----------
			var from = squareB2.Position;
			var toB3 = new BoardPosition(from.Column, from.Row + 1);
			var toB4 = new BoardPosition(from.Column, from.Row + 2);

			var pseudo = new List<Move>
			{
				new(from, toB3, pawnB2.Color, ChessPieceType.Pawn),
				new(from, toB4, pawnB2.Color, ChessPieceType.Pawn)
			};

			// ------------------------- Act ---------------------------------------
			var filtered = rules.FilterLegalMoves(game, pawnB2, pseudo);

			// ------------------------ Assert -------------------------------------
			Assert.That(
				filtered, Is.EquivalentTo(pseudo),
				"Both one-step and two-step forward pawn moves should be legal.");
		}

	#endregion
	}
}
