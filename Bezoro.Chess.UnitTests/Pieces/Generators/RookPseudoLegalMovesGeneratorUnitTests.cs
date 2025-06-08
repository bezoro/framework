using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Pieces.Models;
using Moq;
using NUnit.Framework.Legacy;

namespace Bezoro.Chess.UnitTests.Pieces.Generators;

[TestFixture]
[TestOf(typeof(RookPseudoLegalMovesGenerator))]
public class RookPseudoLegalMovesGeneratorUnitTests
{
	private GameModel                     _gameModel;
	private RookPseudoLegalMovesGenerator _generator;
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
		var emptyBoard = _gameModel.Board.Clear();
		var rook       = emptyBoard.CreatePieceAt("a1", PlayerColor.White, ChessPieceType.Rook);
		Assert.That(rook, Is.Not.Null);
		Assert.That(rook, Is.TypeOf<RookModel>());

		var pseudoMoves = _generator.Generate(_gameModel, rook).ToList();

		Assert.That(pseudoMoves.Count, Is.EqualTo(14), "Rook should have 14 possible moves from a1");

		var expectedDestinations = new[]
		{
			"a2", "a3", "a4", "a5", "a6", "a7", "a8",
			"b1", "c1", "d1", "e1", "f1", "g1", "h1"
		};

		var actualDestinations = pseudoMoves.Select(m => m.To.ToString()).ToList();

		CollectionAssert.AreEquivalent(
			expectedDestinations, actualDestinations,
			"Rook should be able to move vertically up a-file and horizontally across rank 1");
	}

#endregion
}
