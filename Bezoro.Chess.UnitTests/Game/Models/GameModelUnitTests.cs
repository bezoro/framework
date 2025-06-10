using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;
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
		var move = Move.Standard(new("b2"), new("b4"), PlayerColor.White, ChessPieceType.Pawn, MoveKind.Normal);

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

	[Test]
	public void ToFenString_WhenCustomBoard_ReturnsExpectedFenString()
	{
		var game   = new GameModel(FenUtils.EmptyBoard);
		var board  = game.Board;
		var pawn   = game.Board.CreatePieceAt("d2", PlayerColor.White, ChessPieceType.Pawn);
		var knight = game.Board.CreatePieceAt("e4", PlayerColor.White, ChessPieceType.Knight);
		var bishop = game.Board.CreatePieceAt("f5", PlayerColor.White, ChessPieceType.Bishop);
		var rook   = game.Board.CreatePieceAt("a1", PlayerColor.Black, ChessPieceType.Rook);
		game.AddCastlingRights(CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide);
		;

		var fen = game.ToFenString();

		Assert.That(fen, Is.Not.Null);
		Assert.That(fen, Is.Not.Empty);
		Assert.That(fen, Is.EquivalentTo("8/8/8/5B2/4N3/8/3P4/r7 w KQ - 0 1"));
		TestContext.Out.Write($"{fen}");
	}

	[Test]
	public void ToFenString_WhenStandardBoard_ReturnsValidFenString()
	{
		var game = new GameModel();

		var fen = game.ToFenString();
		Assert.That(fen, Is.Not.Null);
		Assert.That(fen, Is.Not.Empty);
		Assert.That(fen, Is.EquivalentTo(FenUtils.START_FEN));
		TestContext.Out.Write($"{fen}");
	}

#endregion
}
