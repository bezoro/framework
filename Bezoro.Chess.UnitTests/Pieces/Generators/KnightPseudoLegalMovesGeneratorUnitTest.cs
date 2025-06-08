using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.UnitTests.Pieces.Generators;

[TestFixture]
[TestOf(typeof(KnightPseudoLegalMovesGenerator))]
public class KnightPseudoLegalMovesGeneratorUnitTest
{
#region Test Methods

	[Test]
	public void Generate_WhenKnightAtCenterOfEmptyBoard_ReturnsCorrectMoves()
	{
		var game   = new GameModel(FenUtils.EmptyBoard);
		var knight = game.Board.CreatePieceAt("d4", PlayerColor.White, ChessPieceType.Knight);
		Assert.That(knight, Is.Not.Null);
		Assert.That(knight, Is.TypeOf<KnightModel>());
		var generator = new KnightPseudoLegalMovesGenerator();

		var pseudoMoves = generator.Generate(game, knight).ToList();

		Assert.That(pseudoMoves, Has.Count.EqualTo(8), "Knight should have 8 possible moves from d4");
		var expectedMoves = new[] { "c6", "e6", "f5", "f3", "e2", "c2", "b3", "b5" };
		var actualMoves   = pseudoMoves.Select(m => m.To.Algebraic).ToArray();
		Assert.That(actualMoves, Is.EquivalentTo(expectedMoves), "Knight moves should match expected positions");
	}

	[Test]
	public void Generate_WhenKnightBlockedByFriendlyPieces_ReturnsCorrectMoves()
	{
		var game   = new GameModel(FenUtils.EmptyBoard);
		var knight = game.Board.CreatePieceAt("d4", PlayerColor.White, ChessPieceType.Knight);
		game.Board.CreatePieceAt("c6", PlayerColor.White, ChessPieceType.Pawn);
		game.Board.CreatePieceAt("f5", PlayerColor.White, ChessPieceType.Pawn);
		var generator = new KnightPseudoLegalMovesGenerator();

		var pseudoMoves = generator.Generate(game, knight).ToList();

		Assert.That(
			pseudoMoves, Has.Count.EqualTo(6), "Knight should have 6 possible moves when blocked by 2 friendly pieces");

		var expectedMoves = new[] { "e6", "f3", "e2", "c2", "b3", "b5" };
		var actualMoves   = pseudoMoves.Select(m => m.To.Algebraic).ToArray();
		Assert.That(
			actualMoves, Is.EquivalentTo(expectedMoves), "Knight moves should exclude squares with friendly pieces");
	}

	[Test]
	public void Generate_WhenKnightCanCapture_ReturnsCorrectMoves()
	{
		var game   = new GameModel(FenUtils.EmptyBoard);
		var knight = game.Board.CreatePieceAt("d4", PlayerColor.White, ChessPieceType.Knight);
		game.Board.CreatePieceAt("c6", PlayerColor.Black, ChessPieceType.Pawn);
		game.Board.CreatePieceAt("f5", PlayerColor.Black, ChessPieceType.Pawn);
		var generator = new KnightPseudoLegalMovesGenerator();

		var pseudoMoves = generator.Generate(game, knight).ToList();

		Assert.That(pseudoMoves, Has.Count.EqualTo(8), "Knight should have 8 possible moves including captures");
		var captureMoves = pseudoMoves.Where(m => m.Kind == MoveKind.Capture).ToList();
		Assert.That(captureMoves, Has.Count.EqualTo(2), "Knight should have 2 capture moves");
		Assert.That(
			captureMoves.Select(m => m.To.Algebraic), Is.EquivalentTo(new[] { "c6", "f5" }),
			"Capture moves should target enemy pieces");
	}

#endregion
}
