using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Pieces.Models;
using Moq;
using NUnit.Framework.Legacy;

namespace Bezoro.Chess.UnitTests.Pieces;

[TestFixture]
public class QueenPseudoValidMovesGeneratorTests
{
	private GameModel                      _gameModel;
	private QueenPseudoValidMovesGenerator _generator;
	private Mock<IChessBoardModel>         _mockBoardModel;
	private QueenModel                     _queenPiece;

#region Setup/Teardown Methods

	[SetUp]
	public void SetUp()
	{
		_generator      = new();
		_gameModel      = new();
		_mockBoardModel = new();
		_queenPiece     = new(PlayerColor.White);

		// Setup the mock board for the game model if we were to use it directly
		// For some tests, we use the concrete BoardModel within GameModel
		_gameModel.SetBoard(_mockBoardModel.Object);
	}

#endregion

#region Test Methods

	[Test]
	public void Generate_NullGame_ThrowsArgumentNullException() =>
		// Act & Assert
		Assert.Throws<ArgumentNullException>(() => _generator.Generate(null, _queenPiece).ToList(), "game");

	[Test]
	public void Generate_NullPiece_ThrowsArgumentNullException() =>
		// Act & Assert
		Assert.Throws<ArgumentNullException>(() => _generator.Generate(_gameModel, null).ToList(), "piece");

	[Test]
	public void Generate_PieceNotQueen_ThrowsArgumentException()
	{
		// Arrange
		var notQueen = new PawnModel(PlayerColor.White); // Any piece other than Queen

		// Act & Assert
		Assert.Throws<ArgumentException>(() => _generator.Generate(_gameModel, notQueen).ToList(), "piece");
	}

	[Test]
	public void Generate_QueenNotOnBoard_ReturnsEmpty()
	{
		// Arrange
		_mockBoardModel.Setup(b => b.GetPosition(It.IsAny<IChessPieceModel>()))
					   .Returns((BoardPosition?)null);

		_gameModel.SetBoard(_mockBoardModel.Object);

		// Act
		var moves = _generator.Generate(_gameModel, _queenPiece).ToList();

		// Assert
		CollectionAssert.IsEmpty(moves);
		_mockBoardModel.Verify(b => b.GetPosition(_queenPiece), Times.Once);
	}

	[Test]
	public void Generate_WhiteQueenOnD1_StandardBoard_ReturnsCorrectNumberOfMoves()
	{
		// Arrange
		var standardGame = new GameModel(); // Fresh standard board
		var whiteQueen   = standardGame.Board.GetPieceAt("d1");

		Assert.That(whiteQueen, Is.Not.Null,             "White queen should be on d1 in a standard setup.");
		Assert.That(whiteQueen, Is.TypeOf<QueenModel>(), "Piece on d1 should be a Queen.");

		// Act
		var pseudoMoves = _generator.Generate(standardGame, whiteQueen).ToList();

		// Assert
		// Expected moves for a queen on d1 on an empty 8x8 board:
		// 7 orthogonal moves along the D file (d2-d8)
		// 7 orthogonal moves along the 1st rank (a1-c1, e1-h1)
		// 7 diagonal moves (a4-c2 up-left, e2-h5 up-right)
		// Total = 7 + 7 + 7 = 21
		// Note: board.GetOrthogonalSquares and board.GetDiagonalSquares return squares *excluding* the start square.
		Assert.That(
			pseudoMoves.Count, Is.EqualTo(21),
			"Queen on d1 should have 21 pseudo-legal moves on a standard board.");

		TestContext.WriteLine($"Successfully generated queen pseudo-valid moves from d1: {pseudoMoves.Count}");
		foreach (var pseudoMove in pseudoMoves)
		{
			TestContext.Out.WriteLine($"{pseudoMove}");
		}
	}

	[Test]
	public void Generate_WhiteQueenOnD4_EmptyBoard_ReturnsCorrectNumberOfMoves()
	{
		// Arrange
		var emptyBoardGame    = new GameModel(FenUtils.EMPTY_FEN); // Create a game with an empty board
		var whiteQueenForTest = new QueenModel(PlayerColor.White);
		var square            = emptyBoardGame.Board.GetSquareAt("d4");
		emptyBoardGame.Board.SetPieceAt(whiteQueenForTest, square);
		var queenOnD4 = emptyBoardGame.Board.GetPieceAt("d4");

		Assert.That(queenOnD4, Is.Not.Null,             "White queen should be on d4.");
		Assert.That(queenOnD4, Is.TypeOf<QueenModel>(), "Piece on d4 should be a Queen.");

		// Act
		var pseudoMoves = _generator.Generate(emptyBoardGame, queenOnD4).ToList();

		// Assert
		// Expected moves for a queen on d4 on an empty 8x8 board:
		// Orthogonal: 7 (d-file) + 7 (4th rank) = 14
		// Diagonal 1 (a1-h8 style): 3 (to a1) + 4 (to h8) = 7
		// Diagonal 2 (a7-h2 style): 3 (to a7) + 3 (to g1 - not h2) = 6 (d4 to g1 is 3 steps, (e3, f2, g1))
		// Total = 14 + 7 + 6 = 27
		Assert.That(
			pseudoMoves.Count, Is.EqualTo(27), "Queen on d4 should have 27 pseudo-legal moves on an empty board.");

		TestContext.WriteLine(
			$"Successfully generated queen pseudo-valid moves from d4 on empty board: {pseudoMoves.Count}");
		// foreach (var pseudoMove in pseudoMoves)
		// {
		// 	TestContext.Out.WriteLine($"{pseudoMove}");
		// }
	}

#endregion
}
