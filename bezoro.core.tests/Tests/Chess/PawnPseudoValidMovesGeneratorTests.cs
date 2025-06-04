using System.Linq;
using Bezoro.Core.Chess;
using Bezoro.Core.Chess.Pieces;
using Bezoro.Core.Chess.Utils;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Chess
{
	[TestFixture]
	public sealed class PawnPseudoValidMovesGeneratorTests
	{
	#region Test Methods

		[Test]
		public void Constructor_CreatesValidInstance()
		{
			// Act
			var generator = new PawnPseudoValidMovesGenerator();

			// Assert
			Assert.Multiple(
				() =>
				{
					Assert.That(generator, Is.Not.Null, "Instance should not be null.");
					Assert.That(
						generator, Is.InstanceOf<PawnPseudoValidMovesGenerator>(),
						"Instance should be of correct concrete type.");

					Assert.That(
						generator, Is.AssignableTo<IPseudoMoveGenerator>(),
						"Instance should implement IPseudoMoveGenerator.");
				});
		}

		[Test]
		public void Generate_StartingPawn_ReturnsAllPseudoValidMoves()
		{
			// Arrange
			var board = new BoardModel();
			var pawn  = board.GetPieceAt("c2");

			// Act
			var moves = pawn.GetPseudoLegalMoves(board).ToList();

			// Assert
			var expectedMoves = new[]
			{
				(Square: "c3", Kind: MoveKind.Normal),
				(Square: "c4", Kind: MoveKind.Normal),
				(Square: "b3", Kind: MoveKind.Capture),
				(Square: "d3", Kind: MoveKind.Capture),
				(Square: "b3", Kind: MoveKind.EnPassant),
				(Square: "d3", Kind: MoveKind.EnPassant)
			};

			Assert.Multiple(
				() =>
				{
					Assert.That(moves, Has.Count.EqualTo(6), "A pawn should generate 6 pseudo-valid moves in total.");

					for (var i = 0 ; i < expectedMoves.Length ; i++)
					{
						var (square, kind) = expectedMoves[i];
						Assert.That(moves[i].To.Algebraic, Is.EqualTo(square), $"Move {i + 1} should be to {square}");
						Assert.That(moves[i].Kind,         Is.EqualTo(kind),   $"Move {i + 1} should be a {kind} move");
					}
				});
		}

	#endregion
	}
}
