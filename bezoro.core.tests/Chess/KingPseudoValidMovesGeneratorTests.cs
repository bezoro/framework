using System.Linq;
using Bezoro.Core.Chess.Board;
using Bezoro.Core.Chess.Common.Enums;
using Bezoro.Core.Chess.Common.Extensions;
using Bezoro.Core.Chess.Game.Models;
using Bezoro.Core.Chess.Moves.Services;
using Bezoro.Core.Chess.Pieces.Models;
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
		public void Generate_KingInCenterE4_ReturnsCorrectStandardMoves()
		{
			// Arrange
			var game = new GameModel();
			game.Board.Clear();

			var king = new KingModel(PlayerColor.Black);
			// Assuming GetSquareAt returns the correct type or BoardPosition.FromString can be used directly
			// if SetPieceAt accepts BoardPosition.
			var kingPosition = game.Board.GetSquareAt("e4");
			game.Board.SetPieceAt(king, kingPosition);

			var generator = new KingPseudoValidMoveGenerator();

			// Act
			var pseudoMoves = generator.Generate(game, king)
									   .Where(m => m.Kind == MoveKind.Normal) // Filter out castling
									   .ToList();

			// Assert
			Assert.That(pseudoMoves.Count, Is.EqualTo(8), "King in the center (e4) should have 8 standard moves.");

			var expectedTargetSquares = new[]
			{
				"d3", "d4", "d5", // Column D
				"e3", "e5",       // Column E (same column, up and down)
				"f3", "f4", "f5"  // Column F
			}.Select(BoardPosition.FromString).ToList();

			var actualTargetSquares = pseudoMoves.Select(m => m.To).ToList();

			Assert.That(
				actualTargetSquares, Is.EquivalentTo(expectedTargetSquares), "King moves from e4 are incorrect.");

			TestContext.Out.WriteLine($"Pseudo moves from e4 ({pseudoMoves.Count}):");
			foreach (var move in pseudoMoves)
			{
				TestContext.Out.WriteLine(move);
			}
		}

		[Test]
		public void Generate_KingInCornerA1_ReturnsCorrectStandardMoves()
		{
			// Arrange
			var game = new GameModel(); // Assumes GameModel() creates a standard board setup
			game.Board.Clear();         // Clear the board for a clean setup

			var king         = new KingModel(PlayerColor.White);
			var kingPosition = game.Board.GetSquareAt("a1");
			game.Board.SetPieceAt(king, kingPosition);

			var generator = new KingPseudoValidMoveGenerator();

			// Act
			var pseudoMoves = generator.Generate(game, king)
									   .Where(m => m.Kind == MoveKind.Normal) // Filter out castling for this test
									   .ToList();

			// Assert
			Assert.That(pseudoMoves.Count, Is.EqualTo(3), "King in a corner should have 3 standard moves.");

			var expectedTargetSquares = new[] { "a2", "b1", "b2" }.Select(BoardPosition.FromString).ToList();
			var actualTargetSquares   = pseudoMoves.Select(m => m.To).ToList();

			Assert.That(
				actualTargetSquares, Is.EquivalentTo(expectedTargetSquares), "King moves from a1 are incorrect.");

			TestContext.Out.WriteLine($"Pseudo moves from a1 ({pseudoMoves.Count}):");
			foreach (var move in pseudoMoves)
			{
				TestContext.Out.WriteLine(move);
			}
		}

		[Test]
		public void Generate_KingOnEdgeA4_ReturnsCorrectStandardMoves()
		{
			// Arrange
			var game = new GameModel();
			game.Board.Clear();

			var king = new KingModel(PlayerColor.White);
			var kingPosition =
				game.Board.GetSquareAt("a4"); // Assuming GetSquareAt returns the correct type for SetPieceAt

			game.Board.SetPieceAt(king, kingPosition);

			var generator = new KingPseudoValidMoveGenerator();

			// Act
			var pseudoMoves = generator.Generate(game, king)
									   .Where(m => m.Kind == MoveKind.Normal) // Filter out castling
									   .ToList();

			// Assert
			Assert.That(pseudoMoves.Count, Is.EqualTo(5), "King on an edge (a4) should have 5 standard moves.");

			var expectedTargetSquares =
				new[] { "a3", "a5", "b3", "b4", "b5" }.Select(BoardPosition.FromString).ToList();

			var actualTargetSquares = pseudoMoves.Select(m => m.To).ToList();

			Assert.That(
				actualTargetSquares, Is.EquivalentTo(expectedTargetSquares), "King moves from a4 are incorrect.");

			TestContext.Out.WriteLine($"Pseudo moves from a4 ({pseudoMoves.Count}):");
			foreach (var move in pseudoMoves)
			{
				TestContext.Out.WriteLine(move);
			}
		}

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
