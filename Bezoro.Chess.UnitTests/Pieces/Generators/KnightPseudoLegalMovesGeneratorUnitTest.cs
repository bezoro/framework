using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Pieces.Models;
using Moq;

namespace Bezoro.Chess.UnitTests.Pieces.Generators;

[TestFixture]
[TestOf(typeof(KnightPseudoLegalMovesGenerator))]
public class KnightPseudoLegalMovesGeneratorUnitTest
{
	private GameModel                       _game;
	private KnightPseudoLegalMovesGenerator _generator;
	private Mock<IChessPieceModel>          _knightMock;

#region Setup/Teardown Methods

	[SetUp]
	public void Setup()
	{
		_generator = new();
		_game      = new(); // Uses standard board setup

		// Mock knight piece
		_knightMock = new();
		_knightMock.Setup(p => p.Color).Returns(PlayerColor.White);
	}

#endregion

#region Test Methods

	[Test]
	public void Generate_WhenGameIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		GameModel game   = null;
		var       knight = _knightMock.Object;

		// Act & Assert
		var ex = Assert.Throws<ArgumentNullException>(() => _generator.Generate(game, knight));
		Assert.That(ex.ParamName, Is.EqualTo("game"));
	}

	[Test]
	public void Generate_WhenPieceIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var              game   = _game;
		IChessPieceModel knight = null;

		// Act & Assert
		var ex = Assert.Throws<ArgumentNullException>(() => _generator.Generate(game, knight));
		Assert.That(ex.ParamName, Is.EqualTo("piece"));
	}

	[Test]
	public void Generate_WhenPieceNotOnBoard_ReturnsEmptyCollection()
	{
		// Arrange
		var knight = _knightMock.Object;
		// The mock piece is not placed on the board

		// Act
		var moves = _generator.Generate(_game, knight);

		// Assert
		Assert.That(moves, Is.Empty);
	}

	[Test]
	public void Generate_WithEnemyPiecesBlocking_IncludesCaptures()
	{
		// Arrange - Place knight and enemy pieces on empty board
		var game  = new GameModel(FenUtils.EmptyBoard);
		var board = game.Board;

		var knight = board.CreatePieceAt("d4", PlayerColor.White, ChessPieceType.Knight);
		board.CreatePieceAt("b3", PlayerColor.Black, ChessPieceType.Pawn);
		board.CreatePieceAt("f5", PlayerColor.Black, ChessPieceType.Pawn);

		// Act
		var moves = _generator.Generate(game, knight).ToList();

		// Assert
		Assert.That(moves, Has.Count.EqualTo(8), "Knight should have 8 possible moves including captures");
		Assert.That(
			moves.Count(m => m.Kind == MoveKind.Capture), Is.EqualTo(2),
			"Should have 2 capture moves");

		Assert.That(
			moves.Any(m => m.To.Algebraic == "b3" && m.Kind == MoveKind.Capture),
			"Should include capture at b3");

		Assert.That(
			moves.Any(m => m.To.Algebraic == "f5" && m.Kind == MoveKind.Capture),
			"Should include capture at f5");
	}

	[Test]
	public void Generate_WithFriendlyPiecesBlocking_ExcludesBlockedSquares()
	{
		// Arrange - Place knight and friendly pieces on empty board
		var game  = new GameModel(FenUtils.EmptyBoard);
		var board = game.Board;

		var knight = board.CreatePieceAt("d4", PlayerColor.White, ChessPieceType.Knight);
		board.CreatePieceAt("b3", PlayerColor.White, ChessPieceType.Pawn);
		board.CreatePieceAt("f5", PlayerColor.White, ChessPieceType.Pawn);

		// Act
		var moves = _generator.Generate(game, knight).ToList();

		// Assert
		Assert.That(
			moves, Has.Count.EqualTo(6), "Knight should have 6 possible moves when blocked by 2 friendly pieces");

		Assert.That(
			moves.All(m => m.To.Algebraic != "b3" && m.To.Algebraic != "f5"),
			"Moves to squares occupied by friendly pieces should be excluded");
	}

	[Test]
	public void Generate_WithKnightInCenter_GeneratesEightMoves()
	{
		// Arrange - Place knight on empty board at center position (e4/d4)
		var game  = new GameModel(FenUtils.EmptyBoard);
		var board = game.Board;

		var knight = board.CreatePieceAt("d4", PlayerColor.White, ChessPieceType.Knight);

		// Act
		var moves = _generator.Generate(game, knight).ToList();

		// Assert
		Assert.That(moves, Has.Count.EqualTo(8), "Knight in center should have 8 possible moves");

		// All expected knight moves from d4 should be present
		var expectedDestinations = new[]
		{
			new BoardPosition(1, 2), // b3
			new BoardPosition(1, 4), // b5
			new BoardPosition(2, 1), // c2
			new BoardPosition(2, 5), // c6
			new BoardPosition(4, 1), // e2
			new BoardPosition(4, 5), // e6
			new BoardPosition(5, 2), // f3
			new BoardPosition(5, 4)  // f5
		};

		foreach (var destination in expectedDestinations)
		{
			Assert.That(
				moves.Any(m => m.To.File == destination.File && m.To.Rank == destination.Rank),
				$"Should have move to {destination.Algebraic}");
		}
	}

#endregion
}
