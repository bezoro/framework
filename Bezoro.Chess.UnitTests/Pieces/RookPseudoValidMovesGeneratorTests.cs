using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Pieces.Models;
using Moq;
using NUnit.Framework.Legacy;

namespace Bezoro.Chess.UnitTests.Pieces;

[TestFixture]
public class RookPseudoValidMovesGeneratorTests
{
	private GameModel                     _gameModel;
	private RookPseudoValidMovesGenerator _generator;
	// Changed from IChessBoardModel to BoardModel
	private Mock<IChessBoardModel> _mockBoardModel;
	private RookModel              _rookPiece;

#region Setup/Teardown Methods

	[SetUp]
	public void SetUp()
	{
		_generator      = new();
		_gameModel      = new();
		_mockBoardModel = new();
		_rookPiece      = new(PlayerColor.White);
	}

#endregion

#region Test Methods

	[Test]
	public void Generate_NullGame_ThrowsArgumentNullException() =>
		Assert.Throws<ArgumentNullException>(() => _generator.Generate(null, _rookPiece).ToList(), "game");

	[Test]
	public void Generate_NullPiece_ThrowsArgumentNullException() =>
		Assert.Throws<ArgumentNullException>(() => _generator.Generate(_gameModel, null).ToList(), "piece");

	[Test]
	public void Generate_PieceNotRook_ThrowsArgumentException()
	{
		var notRook = new PawnModel(PlayerColor.White);

		Assert.Throws<ArgumentException>(
			() => _generator.Generate(_gameModel, notRook).ToList(), "piece");
	}

	[Test]
	public void Generate_RookNotOnBoard_ReturnsEmpty()
	{
		_mockBoardModel.Setup(b => b.GetPosition(It.IsAny<RookModel>())).Returns((BoardPosition?)null);

		var moves = _generator.Generate(_gameModel, _rookPiece).ToList();

		CollectionAssert.IsEmpty(moves);
	}

	[Test]
	public void Generate_RookOnBoard_ReturnsCorrectMoves()
	{
		var rook = _gameModel.Board.GetSquareAt("a1").Piece;
		Assert.That(rook, Is.Not.Null);
		Assert.That(rook, Is.TypeOf<RookModel>());
		var pseudoMoves = _generator.Generate(_gameModel, rook).ToList();
		Assert.That(pseudoMoves, Is.Not.Empty);
		TestContext.WriteLine($"Successfully generated rook pseudo-valid moves: {pseudoMoves.Count}");
		foreach (var pseudoMove in pseudoMoves)
		{
			TestContext.Out.WriteLine($"{pseudoMove}");
		}
	}

#endregion
}
