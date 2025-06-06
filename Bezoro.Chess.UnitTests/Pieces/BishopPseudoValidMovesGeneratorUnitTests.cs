using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.UnitTests.Pieces;

[TestFixture]
[TestOf(typeof(BishopPseudoValidMovesGenerator))]
public class BishopPseudoValidMovesGeneratorUnitTests
{
#region Test Methods

	[Test]
	public void Generate_ValidBishop_ReturnsCorrectMoves()
	{
		// Arrange
		var game   = new GameModel();
		var bishop = game.Board.GetPieceAt("c1");
		Assert.That(bishop, Is.Not.Null);
		var generator = new BishopPseudoValidMovesGenerator();

		// Act
		var pseudoMoves = generator.Generate(game, bishop).ToList();

		// Assert
		Assert.That(pseudoMoves, Is.Not.Empty);
		var expectedMoves = new[] { "b2", "a3", "d2", "e3", "f4", "g5", "h6" };
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
