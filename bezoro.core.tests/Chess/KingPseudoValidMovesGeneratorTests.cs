using System.Linq;
using Bezoro.Core.Chess.Common.Extensions;
using Bezoro.Core.Chess.Game.Models;
using Bezoro.Core.Chess.Moves.Services;
using NUnit.Framework;
// For FenUtils if used

// For IChessBoardModel

namespace Bezoro.Core.Tests.Chess
{
	[TestFixture]
	public class KingPseudoValidMovesGeneratorTests
	{
	#region Test Methods

		[Test]
		public void Generate_ValidKing_ReturnsCorrectMoves()
		{
			// Arrange
			var game = new GameModel();
			var king = game.Board.GetPieceAt("e1");
			Assert.That(king, Is.Not.Null);
			var generator = new KingPseudoValidMoveGenerator();

			// Act
			var pseudoMoves = generator.Generate(game, king).ToList();
			Assert.That(pseudoMoves, Is.Not.Empty);
			;
			TestContext.Out.WriteLine($"Pseudo moved:{pseudoMoves.Count}");
			foreach (var move in pseudoMoves)
			{
				TestContext.Out.WriteLine(move);
			}
		}

	#endregion
	}
}
