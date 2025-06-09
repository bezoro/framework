using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;
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
	public void Execute_WhenPawnCaptures_RemovesTargetPieceAndMovesPawn()
	{
		var game        = new GameModel(FenUtils.EmptyBoard);
		var board       = game.Board;
		var whitePawn   = board.CreatePieceAt("e4", PlayerColor.White, ChessPieceType.Pawn);
		var blackPawn   = board.CreatePieceAt("f5", PlayerColor.Black, ChessPieceType.Pawn);
		var move        = new Move(new("e4"), new("f5"), PlayerColor.White, ChessPieceType.Pawn, MoveKind.Capture);
		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt("f5"), Is.EqualTo(whitePawn));
		// Assert.That(board.GetSquareAt("e4").Piece, Is.Null);
	}

	[Test]
	public void Execute_WhenStartingB2PawnDoublePush_MovesPawnToB4()
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
