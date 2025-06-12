using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Pieces.Generators;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.UnitTests.Pieces.Generators;

[TestFixture]
[TestOf(typeof(KingPseudoLegalMoveGenerator))]
public class KingPseudoLegalMovesGeneratorUnitTests
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

		var generator = new KingPseudoLegalMoveGenerator();

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

		var generator = new KingPseudoLegalMoveGenerator();

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

		var generator = new KingPseudoLegalMoveGenerator();

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
	public void Generate_KingsideCastlingH1RookHasMoved_NoCastlingMoveGenerated()
	{
		// Arrange
		var game = new GameModel();
		game.Board.Clear();

		var king = new KingModel(PlayerColor.White); // King has NOT moved
		// king.MarkMoved(); // Ensure king has not moved for this test
		var kingPosition = game.Board.GetSquareAt("e1");
		game.Board.SetPieceAt(king, kingPosition);

		var rook = new RookModel(PlayerColor.White);
		rook.MarkMoved(); // Mark h1-Rook as having moved
		var rookPosition = game.Board.GetSquareAt("h1");
		game.Board.SetPieceAt(rook, rookPosition);

		// f1 and g1 are implicitly empty due to Board.Clear()

		var generator = new KingPseudoLegalMoveGenerator();

		// Act
		var pseudoMoves = generator.Generate(game, king).ToList();
		var kingsideCastlingMove =
			pseudoMoves.FirstOrDefault(m => m is { Kind: MoveKind.Castle, CastleSide: CastleSide.King });

		// Assert
		// If no CastleKingside move is found, FirstOrDefault returns default(Move).
		// We assert that the Kind of this (potentially default) move is not CastleKingside.
		// This relies on default(Move).Kind not being MoveKind.CastleKingside.
		Assert.That(
			kingsideCastlingMove.Kind, Is.Not.EqualTo(MoveKind.Castle),
			"Castling move should not be generated if the h1-Rook has moved.");

		var standardMovesCount = pseudoMoves.Count(m => m.Kind == MoveKind.Normal);
		Assert.That(standardMovesCount, Is.EqualTo(5), "Standard king moves should still be generated.");

		TestContext.Out.WriteLine($"Pseudo moves generated ({pseudoMoves.Count}):");
		foreach (var move in pseudoMoves)
		{
			TestContext.Out.WriteLine($"{move} (Kind: {move.Kind})");
		}
	}

	[Test]
	public void Generate_KingsideCastlingKingHasMoved_NoCastlingMoveGenerated()
	{
		// Arrange
		var game = new GameModel(); // Assuming GameModel tracks castling rights or piece history
		game.Board.Clear();

		var king = new KingModel(PlayerColor.White);
		king.MarkMoved(); // Mark King as having moved
		var kingPosition = game.Board.GetSquareAt("e1");
		game.Board.SetPieceAt(king, kingPosition);

		var rook         = new RookModel(PlayerColor.White); // Rook has not moved
		var rookPosition = game.Board.GetSquareAt("h1");
		game.Board.SetPieceAt(rook, rookPosition);

		// Ensure squares between King and Rook are empty
		// game.Board.GetSquareAt("f1").Piece = null; // Implicitly empty by Board.Clear()
		// game.Board.GetSquareAt("g1").Piece = null; // Implicitly empty by Board.Clear()

		var generator = new KingPseudoLegalMoveGenerator();

		// Act
		var pseudoMoves = generator.Generate(game, king).ToList();
		var kingsideCastlingMove =
			pseudoMoves.FirstOrDefault(m => m is { Kind: MoveKind.Castle, CastleSide: CastleSide.King });

		// Assert
		Assert.That(
			kingsideCastlingMove.Kind, Is.Not.EqualTo(MoveKind.Castle),
			"Castling move should not be generated if the King has moved.");

		// Optional: Verify standard moves are still generated (e.g., 5 if e1 is unblocked)
		// This depends on whether this generator is solely for castling or all king moves.
		// Based on its name "KingPseudoLegalMoveGenerator", it should likely generate all.
		var standardMovesCount = pseudoMoves.Count(m => m.Kind == MoveKind.Normal);
		// Assuming d1, d2, e2, f1, f2 are the targets for standard moves from e1
		// and none are blocked for the purpose of this generator.
		// If 'h1' (rook's square) is considered blocked for a standard move to 'f1', adjust count.
		// Given our clarification, pseudo-moves don't check for friendly pieces on target squares.
		// King on e1 can move to d1, d2, e2, f1, f2.
		Assert.That(standardMovesCount, Is.EqualTo(5), "Standard king moves should still be generated.");

		TestContext.Out.WriteLine($"Pseudo moves generated ({pseudoMoves.Count}):");
		foreach (var move in pseudoMoves)
		{
			TestContext.Out.WriteLine($"{move} (Kind: {move.Kind})");
		}
	}

	[Test]
	public void Generate_QueensideCastlingA1RookHasMoved_NoCastlingMoveGenerated()
	{
		// Arrange
		var game = new GameModel();
		game.Board.Clear();

		var king = new KingModel(PlayerColor.White); // King has NOT moved
		// king.MarkMoved(); 
		var kingPosition = game.Board.GetSquareAt("e1");
		game.Board.SetPieceAt(king, kingPosition);

		var rook = new RookModel(PlayerColor.White);
		rook.MarkMoved(); // Mark a1-Rook as having moved
		var rookPosition = game.Board.GetSquareAt("a1");
		game.Board.SetPieceAt(rook, rookPosition);

		// b1, c1, d1 are implicitly empty due to Board.Clear()

		var generator = new KingPseudoLegalMoveGenerator();

		// Act
		var pseudoMoves = generator.Generate(game, king).ToList();
		var queensideCastlingMove =
			pseudoMoves.FirstOrDefault(m => m is { Kind: MoveKind.Castle, CastleSide: CastleSide.Queen });

		;

		// Assert
		Assert.That(
			queensideCastlingMove.Kind, Is.Not.EqualTo(MoveKind.Castle),
			"Castling move should not be generated if the a1-Rook has moved.");

		var standardMovesCount = pseudoMoves.Count(m => m.Kind == MoveKind.Normal);
		// Standard moves from e1: d1, d2, e2, f1, f2 (5 moves)
		Assert.That(standardMovesCount, Is.EqualTo(5), "Standard king moves should still be generated.");

		TestContext.Out.WriteLine($"Pseudo moves generated ({pseudoMoves.Count}):");
		foreach (var move in pseudoMoves)
		{
			TestContext.Out.WriteLine($"{move} (Kind: {move.Kind})");
		}
	}

	[Test]
	public void Generate_QueensideCastlingKingHasMoved_NoCastlingMoveGenerated()
	{
		// Arrange
		var game = new GameModel();
		game.Board.Clear();

		var king = new KingModel(PlayerColor.White);
		king.MarkMoved(); // Mark King as having moved
		var kingPosition = game.Board.GetSquareAt("e1");
		game.Board.SetPieceAt(king, kingPosition);

		var rook = new RookModel(PlayerColor.White); // a1-Rook has not moved
		// rook.MarkMoved(); 
		var rookPosition = game.Board.GetSquareAt("a1");
		game.Board.SetPieceAt(rook, rookPosition);

		// b1, c1, d1 are implicitly empty due to Board.Clear()

		var generator = new KingPseudoLegalMoveGenerator();

		// Act
		var pseudoMoves = generator.Generate(game, king).ToList();
		var queensideCastlingMove =
			pseudoMoves.FirstOrDefault(m => m is { Kind: MoveKind.Castle, CastleSide: CastleSide.Queen });

		// Assert
		Assert.That(
			queensideCastlingMove.Kind, Is.Not.EqualTo(MoveKind.Castle),
			"Castling move should not be generated if the King has moved.");

		var standardMovesCount = pseudoMoves.Count(m => m.Kind == MoveKind.Normal);
		// Standard moves from e1: d1, d2, e2, f1, f2 (5 moves)
		Assert.That(standardMovesCount, Is.EqualTo(5), "Standard king moves should still be generated.");

		TestContext.Out.WriteLine($"Pseudo moves generated ({pseudoMoves.Count}):");
		foreach (var move in pseudoMoves)
		{
			TestContext.Out.WriteLine($"{move} (Kind: {move.Kind})");
		}
	}

	[Test]
	public void Generate_StartingKing_ReturnsNoMoves()
	{
		// Arrange
		var game = new GameModel();
		var king = game.Board.GetPieceAt("e1");
		Assert.That(king, Is.Not.Null);
		var generator = new KingPseudoLegalMoveGenerator();

		// Act
		var pseudoMoves = generator.Generate(game, king).ToList();
		Assert.That(pseudoMoves, Is.Empty);

		TestContext.Out.WriteLine($"Pseudo moved:{pseudoMoves.Count}");
		foreach (var move in pseudoMoves)
		{
			TestContext.Out.WriteLine(move);
		}
	}

#endregion
}
