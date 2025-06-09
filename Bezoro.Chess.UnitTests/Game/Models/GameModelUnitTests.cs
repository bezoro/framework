using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;

namespace Bezoro.Chess.UnitTests.Game.Models;

[TestFixture]
[TestOf(typeof(GameModel))]
public class GameModelUnitTests
{
#region Test Methods

	[Test]
	public void DoMove_WhenFromB2ToB4PawnMove_PawnMovesToB4()
	{
		var game = new GameModel();
		var move = new Move(new("b2"), new("b4"), PlayerColor.White, ChessPieceType.Pawn);

		game.DoMove(move);

		Assert.That(game.Board.GetPieceAt("b4"), Is.Not.Null);
	}

	[Test]
	public void StartMove_WhenB2Notation_ReturnsCorrectPawnAndMoves()
	{
		var game          = new GameModel();
		var expectedMoves = new[] { "b3", "b4" };

		var moves = game.StartMove("b2");

		Assert.Multiple(
			() =>
			{
				Assert.That(moves.Select(m => m.PieceType),         Is.All.EqualTo(ChessPieceType.Pawn));
				Assert.That(moves,                                  Is.Not.Empty);
				Assert.That(moves.Select(m => m.To.Algebraic),      Is.EquivalentTo(expectedMoves));
				Assert.That(moves.Select(m => m.Kind),              Is.All.EqualTo(MoveKind.Normal));
				Assert.That(moves.Select(m => m.LeavesKingInCheck), Is.All.EqualTo(false));
			});
	}

#endregion
}
