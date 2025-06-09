using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;
using Bezoro.Chess.Pieces.Commands;

namespace Bezoro.Chess.UnitTests.Pieces.Commands;

[TestFixture]
[TestOf(typeof(MovePieceCommand))]
public class MovePieceCommandUnitTests
{
#region Test Methods

	[Test]
	public void Execute_WhenStartingB2PawnDoublePush_MovesPawnTwoSpaces()
	{
		var game        = new GameModel();
		var board       = game.Board;
		var b2Pawn      = board.GetPieceAt("b2");
		var move        = new Move(new("b2"), new("b4"), PlayerColor.White, ChessPieceType.Pawn);
		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt("b4"), Is.EqualTo(b2Pawn));
	}

#endregion
}
