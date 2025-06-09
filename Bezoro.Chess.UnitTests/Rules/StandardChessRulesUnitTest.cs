using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;
using Bezoro.Chess.Rules;

namespace Bezoro.Chess.UnitTests.Rules;

[TestFixture]
[TestOf(typeof(StandardChessRules))]
public class StandardChessRulesUnitTest
{
#region Test Methods

	[Test]
	public void FilterLegalMoves_WhenWouldLeaveKingInCheck_CorrectlyReturnsFlaggedMove()
	{
		var rules = new StandardChessRules();
		var game  = new GameModel();

		var move = new Move(
			new("e2"),
			new("e4"),
			PlayerColor.White,
			ChessPieceType.Pawn,
			leavesKingInCheck: true);

		var result = rules.FilterLegalMoves(game, [ move ]);

		Assert.That(result.Single().LeavesKingInCheck, Is.True);
	}

#endregion
}
