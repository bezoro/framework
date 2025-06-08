using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.UnitTests.Pieces.Generators;

[TestFixture]
[TestOf(typeof(QueenPseudoLegalMovesGenerator))]
public class QueenPseudoLegalMovesGeneratorUnitTests
{
#region Test Methods

	[Test]
	public void Generate_WhenQueenAtCenterOfEmptyBoard_ReturnsCorrectMoves()
	{
		var game  = new GameModel(FenUtils.EmptyBoard);
		var queen = game.Board.CreatePieceAt("d4", PlayerColor.White, ChessPieceType.Queen);
		Assert.That(queen, Is.Not.Null);
		Assert.That(queen, Is.TypeOf<QueenModel>());
		var generator = new QueenPseudoLegalMovesGenerator();

		var pseudoMoves = generator.Generate(game, queen).ToList();
		Assert.That(pseudoMoves, Has.Count.EqualTo(27), "Queen should have 27 possible moves from d4");
		var expectedMoves = new[]
		{
			// Vertical moves (North)
			"d5", "d6", "d7", "d8",
			// Vertical moves (South)
			"d3", "d2", "d1",
			// Horizontal moves (East)
			"e4", "f4", "g4", "h4",
			// Horizontal moves (West)
			"c4", "b4", "a4",
			// Diagonal moves (Northeast)
			"e5", "f6", "g7", "h8",
			// Diagonal moves (Southeast)
			"e3", "f2", "g1",
			// Diagonal moves (Northwest)
			"c5", "b6", "a7",
			// Diagonal moves (Southwest)
			"c3", "b2", "a1"
		};

		var actualMoves = pseudoMoves.Select(m => m.To.Algebraic).ToArray();
		Assert.That(actualMoves, Is.EquivalentTo(expectedMoves), "Queen moves should match expected positions");
	}

#endregion
}
