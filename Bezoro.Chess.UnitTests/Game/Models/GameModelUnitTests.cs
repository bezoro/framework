using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.UnitTests.Game.Models;

[TestFixture]
[TestOf(typeof(GameModel))]
public class GameModelUnitTests
{
#region Test Methods

	[Test]
	public void StartMove_WhenB2Notation_ReturnsCorrectPawnAndMoves()
	{
		var game          = new GameModel();
		var expectedMoves = new[] { "b3", "b4" };

		var (piece, moves) = game.StartMove("b2");

		Assert.Multiple(
			() =>
			{
				Assert.That(piece,                             Is.TypeOf<PawnModel>());
				Assert.That(moves,                             Is.Not.Empty);
				Assert.That(moves.Select(m => m.To.Algebraic), Is.EquivalentTo(expectedMoves));
				Assert.That(moves.Select(m => m.Kind),         Is.All.EqualTo(MoveKind.Normal));
			});
	}

#endregion
}
