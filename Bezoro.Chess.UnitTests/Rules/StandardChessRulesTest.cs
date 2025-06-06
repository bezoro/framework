using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;
using Bezoro.Chess.Pieces.Models;
using Bezoro.Chess.Rules;

namespace Bezoro.Chess.UnitTests.Rules;

[TestFixture]
[TestOf(typeof(StandardChessRules))]
public class StandardChessRulesTest
{
#region Test Methods

	[Test]
	public void FilterLegalMoves_WhenMixedMoves_ReturnsCorrectMoves()
	{
		// Arrange
		var               gameRules   = new StandardChessRules();
		var pseudoMoves = new List<Move>();
		var    legalMoves = new List<Move>();
		IChessPieceModel  piece       = new RookModel(PlayerColor.White);
		var               game        = new GameModel(FenUtils.EmptyBoard);
		var               board       = game.Board;
		var rookToCheck = board.CreatePieceAt("a1",PlayerColor.White,ChessPieceType.Rook);
		board.CreatePieceAt("a2",PlayerColor.White,ChessPieceType.Pawn);
		
		// Act
		pseudoMoves = rookToCheck.GetPseudoLegalMoves(game).ToList();
		legalMoves = gameRules.FilterLegalMoves(game, piece, pseudoMoves).ToList();

		// Assert
		Assert.That(legalMoves, Is.Not.Empty);
		TestContext.Out.WriteLine($"Number of legal moves: {legalMoves.Count}");
		foreach (var move in legalMoves)
		{
			TestContext.Out.WriteLine(move);
		}
	}

#endregion
}
