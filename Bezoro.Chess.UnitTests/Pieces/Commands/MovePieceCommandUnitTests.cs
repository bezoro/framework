using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;
using Bezoro.Chess.Pieces.Commands;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.UnitTests.Pieces.Commands;

[TestFixture]
[TestOf(typeof(MovePieceCommand))]
public class MovePieceCommandUnitTests
{
#region Test Methods

	[Test]
	public void Execute_WhenMoveIsCastleBlackKingSideBlack_MovedRookToF8AndKingToG8()
	{
		var game  = new GameModel(FenUtils.EmptyBoard);
		var board = game.Board;
		var rook  = board.CreatePieceAt("h8", PlayerColor.Black, ChessPieceType.Rook);
		var king  = board.CreatePieceAt("e8", PlayerColor.Black, ChessPieceType.King);
		var move  = Move.CastleKingSide(new("e8"), new("g8"), PlayerColor.Black);

		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt("g8"), Is.TypeOf<KingModel>());
		Assert.That(board.GetPieceAt("f8"), Is.TypeOf<RookModel>());
		Assert.That(board.GetPieceAt("h8"), Is.Null);
		Assert.That(board.GetPieceAt("e8"), Is.Null);
	}

	[Test]
	public void Execute_WhenMoveIsCastleBlackQueenSideBlack_MovesRookToD8AndKingToC8()
	{
		var game  = new GameModel(FenUtils.EmptyBoard);
		var board = game.Board;
		var rook  = board.CreatePieceAt("a8", PlayerColor.Black, ChessPieceType.Rook);
		var king  = board.CreatePieceAt("e8", PlayerColor.Black, ChessPieceType.King);
		var move  = Move.CastleQueenSide(new("e8"), new("c8"), PlayerColor.Black);

		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt("c8"), Is.TypeOf<KingModel>());
		Assert.That(board.GetPieceAt("d8"), Is.TypeOf<RookModel>());
		Assert.That(board.GetPieceAt("a8"), Is.Null);
		Assert.That(board.GetPieceAt("e8"), Is.Null);
	}

	[Test]
	public void Execute_WhenMoveIsCastleWhiteKingSide_MovedRookToF1AndKingToG1()
	{
		var game  = new GameModel(FenUtils.EmptyBoard);
		var board = game.Board;
		var rook  = board.CreatePieceAt("h1", PlayerColor.White, ChessPieceType.Rook);
		var king  = board.CreatePieceAt("e1", PlayerColor.White, ChessPieceType.King);
		var move  = Move.CastleKingSide(new("e1"), new("g1"), PlayerColor.White);

		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt("g1"), Is.TypeOf<KingModel>());
		Assert.That(board.GetPieceAt("f1"), Is.TypeOf<RookModel>());
		Assert.That(board.GetPieceAt("h1"), Is.Null);
		Assert.That(board.GetPieceAt("e1"), Is.Null);
	}

	[Test]
	public void Execute_WhenMoveIsCastleWhiteQueenSide_MovesRookToD1AndKingToC1()
	{
		var game  = new GameModel(FenUtils.EmptyBoard);
		var board = game.Board;
		var rook  = board.CreatePieceAt("a1", PlayerColor.White, ChessPieceType.Rook);
		var king  = board.CreatePieceAt("e1", PlayerColor.White, ChessPieceType.King);
		var move  = Move.CastleQueenSide(new("e1"), new("c1"), PlayerColor.White);

		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt("c1"), Is.TypeOf<KingModel>());
		Assert.That(board.GetPieceAt("d1"), Is.TypeOf<RookModel>());
		Assert.That(board.GetPieceAt("a1"), Is.Null);
		Assert.That(board.GetPieceAt("e1"), Is.Null);
	}

	[Test]
	public void Execute_WhenMoveIsEnPassant_RemovesTargetPieceAndMovesPawn()
	{
		var game  = new GameModel(FenUtils.EmptyBoard);
		var board = game.Board;
		board.SetEnPassantTargetSquare(board.GetSquareAt("f6"));
		var whitePawn = board.CreatePieceAt("e5", PlayerColor.White, ChessPieceType.Pawn);
		var blackPawn = board.CreatePieceAt("f5", PlayerColor.Black, ChessPieceType.Pawn);
		var move = Move.Standard(new("e5"), new("f6"), PlayerColor.White, ChessPieceType.Pawn, MoveKind.EnPassant);
		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);
		Assert.Multiple(
			() =>
			{
				Assert.That(board.GetPieceAt("f6"),        Is.EqualTo(whitePawn));
				Assert.That(board.GetSquareAt("e4").Piece, Is.Null);
			});
	}

	[Test]
	public void Execute_WhenPawnCaptures_RemovesTargetPieceAndMovesPawn()
	{
		var game        = new GameModel(FenUtils.EmptyBoard);
		var board       = game.Board;
		var whitePawn   = board.CreatePieceAt("e4", PlayerColor.White, ChessPieceType.Pawn);
		var blackPawn   = board.CreatePieceAt("f5", PlayerColor.Black, ChessPieceType.Pawn);
		var move        = Move.Standard(new("e4"), new("f5"), PlayerColor.White, ChessPieceType.Pawn, MoveKind.Capture);
		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt("f5"),        Is.EqualTo(whitePawn));
		Assert.That(board.GetSquareAt("e4").Piece, Is.Null);
	}

	[Test]
	public void Execute_WhenStartingB2PawnDoublePush_MovesPawnToB4()
	{
		var game        = new GameModel();
		var board       = game.Board;
		var b2Pawn      = board.GetPieceAt("b2");
		var move        = Move.Standard(new("b2"), new("b4"), PlayerColor.White, ChessPieceType.Pawn, MoveKind.Normal);
		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt("b4"),          Is.EqualTo(b2Pawn));
		Assert.That(board.GetPieceAt("b2"),          Is.Null);
		Assert.That(board.GetPieceAt("b4").HasMoved, Is.True);
	}

	[Test]
	public void Undo_WhenPawnMoved_ReturnsToPastSquare()
	{
		var game        = new GameModel();
		var board       = game.Board;
		var whitePawn   = board.GetPieceAt("a2");
		var move        = Move.Standard(new("a2"), new("a4"), PlayerColor.White, ChessPieceType.Pawn, MoveKind.Normal);
		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);
		Assert.Multiple(
			() =>
			{
				Assert.That(board.GetPieceAt("a4"),          Is.EqualTo(whitePawn));
				Assert.That(board.GetPieceAt("a2"),          Is.Null);
				Assert.That(board.GetPieceAt("a4").HasMoved, Is.True);
			});

		moveCommand.Undo(game);
		Assert.Multiple(
			() =>
			{
				Assert.That(board.GetPieceAt("a2"),          Is.EqualTo(whitePawn));
				Assert.That(board.GetPieceAt("a4"),          Is.Null);
				Assert.That(board.GetPieceAt("a2").HasMoved, Is.False);
			});
	}

	[TestCase(PlayerColor.White)]
	[TestCase(PlayerColor.Black)]
	public void Execute_WhenValidKingSideCastle_MovesKingAndRook(PlayerColor color)
	{
		var game             = new GameModel(FenUtils.EmptyBoard);
		var board            = game.Board;
		var kingSquare       = color == PlayerColor.White ? "e1" : "e8";
		var rookSquare       = color == PlayerColor.White ? "h1" : "h8";
		var targetKingSquare = color == PlayerColor.White ? "g1" : "g8";
		var targetRookSquare = color == PlayerColor.White ? "f1" : "f8";
		board.CreatePieceAt(kingSquare, color, ChessPieceType.King);
		board.CreatePieceAt(rookSquare, color, ChessPieceType.Rook);
		var move        = Move.CastleKingSide(new(kingSquare), new(targetKingSquare), color);
		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt(targetKingSquare), Is.TypeOf<KingModel>());
		Assert.That(board.GetPieceAt(targetRookSquare), Is.TypeOf<RookModel>());
		Assert.That(board.GetPieceAt(rookSquare),       Is.Null);
		Assert.That(board.GetPieceAt(kingSquare),       Is.Null);
	}

	[TestCase(PlayerColor.White)]
	[TestCase(PlayerColor.Black)]
	public void Execute_WhenValidPromotionCapture_PerformsPromotion(PlayerColor color)
	{
		var game        = new GameModel(FenUtils.EmptyBoard);
		var board       = game.Board;
		var startSquare = color == PlayerColor.White ? "a7" : "a2";
		var endSquare   = color == PlayerColor.White ? "b8" : "b1";
		var whitePawn   = board.CreatePieceAt(startSquare, color, ChessPieceType.Pawn);
		var blackPawn   = board.CreatePieceAt(endSquare,   color, ChessPieceType.Pawn);
		var move        = Move.PromotionCapture(new(startSquare), new(endSquare), color, PromotionPieceType.Queen);
		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt(endSquare),   Is.TypeOf<QueenModel>());
		Assert.That(board.GetPieceAt(startSquare), Is.Null);
	}

	[TestCase(PlayerColor.White)]
	[TestCase(PlayerColor.Black)]
	public void Execute_WhenValidPromotionQuiet_PerformsPromotion(PlayerColor color)
	{
		var game        = new GameModel(FenUtils.EmptyBoard);
		var board       = game.Board;
		var startSquare = color == PlayerColor.White ? "a7" : "a2";
		var endSquare   = color == PlayerColor.White ? "a8" : "a1";
		var pawn        = board.CreatePieceAt(startSquare, color, ChessPieceType.Pawn);
		var move        = Move.PromotionQuiet(new(startSquare), new(endSquare), color, PromotionPieceType.Queen);
		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt(endSquare),   Is.TypeOf<QueenModel>());
		Assert.That(board.GetPieceAt(startSquare), Is.Null);
	}

	[TestCase(PlayerColor.White)]
	[TestCase(PlayerColor.Black)]
	public void Execute_WhenValidQueenSideCastle_MovesKingAndRook(PlayerColor color)
	{
		var game             = new GameModel(FenUtils.EmptyBoard);
		var board            = game.Board;
		var kingSquare       = color == PlayerColor.White ? "e1" : "e8";
		var rookSquare       = color == PlayerColor.White ? "a1" : "a8";
		var targetKingSquare = color == PlayerColor.White ? "c1" : "c8";
		var targetRookSquare = color == PlayerColor.White ? "d1" : "d8";
		board.CreatePieceAt(kingSquare, color, ChessPieceType.King);
		board.CreatePieceAt(rookSquare, color, ChessPieceType.Rook);
		var move        = Move.CastleQueenSide(new(kingSquare), new(targetKingSquare), color);
		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt(targetKingSquare), Is.TypeOf<KingModel>());
		Assert.That(board.GetPieceAt(targetRookSquare), Is.TypeOf<RookModel>());
		Assert.That(board.GetPieceAt(rookSquare),       Is.Null);
		Assert.That(board.GetPieceAt(kingSquare),       Is.Null);
	}

	[TestCase(PlayerColor.White)]
	[TestCase(PlayerColor.Black)]
	public void Undo_WhenValidKingSideCastle_ResetsKingAndRook(PlayerColor color)
	{
		var game             = new GameModel(FenUtils.EmptyBoard);
		var board            = game.Board;
		var kingSquare       = color == PlayerColor.White ? "e1" : "e8";
		var rookSquare       = color == PlayerColor.White ? "h1" : "h8";
		var targetKingSquare = color == PlayerColor.White ? "g1" : "g8";
		var targetRookSquare = color == PlayerColor.White ? "f1" : "f8";
		board.CreatePieceAt(kingSquare, color, ChessPieceType.King);
		board.CreatePieceAt(rookSquare, color, ChessPieceType.Rook);
		var move        = Move.CastleKingSide(new(kingSquare), new(targetKingSquare), color);
		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt(targetKingSquare), Is.TypeOf<KingModel>());
		Assert.That(board.GetPieceAt(targetRookSquare), Is.TypeOf<RookModel>());
		Assert.That(board.GetPieceAt(rookSquare),       Is.Null);
		Assert.That(board.GetPieceAt(kingSquare),       Is.Null);

		moveCommand.Undo(game);

		Assert.That(board.GetPieceAt(kingSquare),       Is.TypeOf<KingModel>());
		Assert.That(board.GetPieceAt(targetKingSquare), Is.Null);

		Assert.That(board.GetPieceAt(rookSquare),       Is.TypeOf<RookModel>());
		Assert.That(board.GetPieceAt(targetRookSquare), Is.Null);
	}

	[TestCase(PlayerColor.White)]
	[TestCase(PlayerColor.Black)]
	public void Undo_WhenValidPromotionCapture_UndoesPromotion(PlayerColor color)
	{
		var game        = new GameModel(FenUtils.EmptyBoard);
		var board       = game.Board;
		var startSquare = color == PlayerColor.White ? "a7" : "a2";
		var endSquare   = color == PlayerColor.White ? "b8" : "b1";
		var whitePawn   = board.CreatePieceAt(startSquare, color, ChessPieceType.Pawn);
		var blackPawn   = board.CreatePieceAt(endSquare,   color, ChessPieceType.Pawn);
		var move        = Move.PromotionCapture(new(startSquare), new(endSquare), color, PromotionPieceType.Queen);
		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt(endSquare),   Is.TypeOf<QueenModel>());
		Assert.That(board.GetPieceAt(startSquare), Is.Null);

		moveCommand.Undo(game);

		Assert.That(board.GetPieceAt(endSquare),   Is.TypeOf<PawnModel>());
		Assert.That(board.GetPieceAt(startSquare), Is.TypeOf<PawnModel>());
	}

	[TestCase(PlayerColor.White)]
	[TestCase(PlayerColor.Black)]
	public void Undo_WhenValidPromotionQuiet_UndoesPromotion(PlayerColor color)
	{
		var game        = new GameModel(FenUtils.EmptyBoard);
		var board       = game.Board;
		var startSquare = color == PlayerColor.White ? "a7" : "a2";
		var endSquare   = color == PlayerColor.White ? "a8" : "a1";
		var pawn        = board.CreatePieceAt(startSquare, color, ChessPieceType.Pawn);
		var move        = Move.PromotionQuiet(new(startSquare), new(endSquare), color, PromotionPieceType.Queen);
		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt(endSquare),   Is.TypeOf<QueenModel>());
		Assert.That(board.GetPieceAt(startSquare), Is.Null);

		moveCommand.Undo(game);

		Assert.That(board.GetPieceAt(endSquare),   Is.Null);
		Assert.That(board.GetPieceAt(startSquare), Is.TypeOf<PawnModel>());
	}

	[TestCase(PlayerColor.White)]
	[TestCase(PlayerColor.Black)]
	public void Undo_WhenValidQueenSideCastle_ResetsKingAndRook(PlayerColor color)
	{
		var game             = new GameModel(FenUtils.EmptyBoard);
		var board            = game.Board;
		var kingSquare       = color == PlayerColor.White ? "e1" : "e8";
		var rookSquare       = color == PlayerColor.White ? "a1" : "a8";
		var targetKingSquare = color == PlayerColor.White ? "c1" : "c8";
		var targetRookSquare = color == PlayerColor.White ? "d1" : "d8";
		board.CreatePieceAt(kingSquare, color, ChessPieceType.King);
		board.CreatePieceAt(rookSquare, color, ChessPieceType.Rook);
		var move        = Move.CastleQueenSide(new(kingSquare), new(targetKingSquare), color);
		var moveCommand = new MovePieceCommand(move);

		moveCommand.Execute(game);

		Assert.That(board.GetPieceAt(targetKingSquare), Is.TypeOf<KingModel>());
		Assert.That(board.GetPieceAt(targetRookSquare), Is.TypeOf<RookModel>());
		Assert.That(board.GetPieceAt(rookSquare),       Is.Null);
		Assert.That(board.GetPieceAt(kingSquare),       Is.Null);

		moveCommand.Undo(game);

		Assert.That(board.GetPieceAt(kingSquare),       Is.TypeOf<KingModel>());
		Assert.That(board.GetPieceAt(targetKingSquare), Is.Null);

		Assert.That(board.GetPieceAt(rookSquare),       Is.TypeOf<RookModel>());
		Assert.That(board.GetPieceAt(targetRookSquare), Is.Null);
	}

#endregion
}
