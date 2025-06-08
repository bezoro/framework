using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.UnitTests.Pieces.Generators;

[TestFixture]
[TestOf(typeof(BishopPseudoLegalMovesGenerator))]
public class BishopPseudoLegalMovesGeneratorUnitTests
{
#region Test Methods

	[Test]
	public void Generate_BishopAtCenterOfEmptyBoard_ReturnsCorrectMoves()
	{
		// Arrange
		var game   = new GameModel(FenUtils.EmptyBoard);
		var bishop = game.Board.CreatePieceAt("d4", PlayerColor.White, ChessPieceType.Bishop);
		Assert.That(bishop, Is.Not.Null);
		var generator = new BishopPseudoLegalMovesGenerator();

		// Act
		var pseudoMoves = generator.Generate(game, bishop).ToList();

		// Assert
		Assert.That(pseudoMoves, Is.Not.Empty);
		var expectedMoves = new[]
		{
			"a1", "b2", "c3",      // Down-Left diagonal
			"g1", "f2", "e3",      // Down-Right diagonal
			"a7", "b6", "c5",      // Up-Left diagonal
			"h8", "g7", "f6", "e5" // Up-Right diagonal
		};

		Assert.That(
			pseudoMoves.Select(m => m.To.ToString()),
			Is.EquivalentTo(expectedMoves),
			"Bishop's pseudo-valid moves should match expected diagonal positions");

		TestContext.Out.WriteLine($"Number of pseudo moves: {pseudoMoves.Count}");
		foreach (var pseudoMove in pseudoMoves)
		{
			TestContext.Out.WriteLine(pseudoMove);
		}
	}

#endregion
}
