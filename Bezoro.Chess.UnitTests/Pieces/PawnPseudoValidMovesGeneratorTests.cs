using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Services;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.UnitTests.Pieces;

[TestFixture]
public sealed class PawnPseudoValidMovesGeneratorTests
{
	private GameModel _emptyGame    = null!;
	private GameModel _standardGame = null!;

#region Setup/Teardown Methods

	[SetUp]
	public void Setup()
	{
		_standardGame = new(FenUtils.StartPieces);
		_emptyGame    = new(FenUtils.EmptyBoard);
	}

#endregion

#region Test Methods

	[Test]
	public void Constructor_CreatesValidInstance()
	{
		// Act
		var generator = new PawnPseudoValidMovesGenerator();

		// Assert
		Assert.Multiple(
			() =>
			{
				Assert.That(generator, Is.Not.Null, "Instance should not be null.");
				Assert.That(
					generator, Is.InstanceOf<PawnPseudoValidMovesGenerator>(),
					"Instance should be of correct concrete type.");

				Assert.That(
					generator, Is.AssignableTo<IPseudoMoveGenerator>(),
					"Instance should implement IPseudoMoveGenerator.");
			});
	}

	[Test]
	public void Generate_AfterPawnHasMoved_ExcludesDoubleAdvance()
	{
		var pawn = _standardGame.Board.GetPieceAt("e2");
		pawn.MarkMoved(); // simulate that the pawn already moved

		var moves = pawn.GetPseudoLegalMoves(_standardGame).ToList();

		Assert.That(
			moves.Any(m => m.To.Algebraic == "e4"), Is.False,
			"Double advance should not be generated after the pawn has moved.");
	}

	[Test]
	public void Generate_BlackPawnCanCaptureWhitePawnEnPassantFromLeft()
	{
		// Arrange
		var game      = new GameModel(FenUtils.EmptyBoard);
		var blackPawn = game.Board.CreatePieceAt("e4", PlayerColor.Black, ChessPieceType.Pawn);
		var whitePawn = game.Board.CreatePieceAt("f4", PlayerColor.White, ChessPieceType.Pawn);
		game.Board.SetEnPassantTargetSquare(game.Board.GetSquareAt("f3"));

		// Act
		var moves = blackPawn.GetPseudoLegalMoves(game).ToList();

		// Assert
		Assert.Multiple(
			() =>
			{
				Assert.That(moves.Any(m => m.Kind == MoveKind.EnPassant), Is.True, "Should include en passant capture");
				Assert.That(moves.Any(m => m.To.Algebraic == "f3"), Is.True, "Should include move to f3 square");
			});
	}

	[Test]
	public void Generate_BlackPawnCanCaptureWhitePawnEnPassantFromRight()
	{
		// Arrange
		var game      = new GameModel(FenUtils.EmptyBoard);
		var blackPawn = game.Board.CreatePieceAt("c4", PlayerColor.Black, ChessPieceType.Pawn);
		var whitePawn = game.Board.CreatePieceAt("b4", PlayerColor.White, ChessPieceType.Pawn);
		game.Board.SetEnPassantTargetSquare(game.Board.GetSquareAt("b3"));

		// Act
		var moves = blackPawn.GetPseudoLegalMoves(game).ToList();

		// Assert
		Assert.Multiple(
			() =>
			{
				Assert.That(moves.Any(m => m.Kind == MoveKind.EnPassant), Is.True, "Should include en passant capture");
				Assert.That(moves.Any(m => m.To.Algebraic == "b3"), Is.True, "Should include move to b3 square");
			});
	}

	[Test]
	public void Generate_NoEnPassantPossibleWhenTargetSquareNotSet()
	{
		// Arrange
		var game      = new GameModel(FenUtils.EmptyBoard);
		var whitePawn = game.Board.CreatePieceAt("e5", PlayerColor.White, ChessPieceType.Pawn);
		var blackPawn = game.Board.CreatePieceAt("f5", PlayerColor.Black, ChessPieceType.Pawn);

		// Act
		var moves = whitePawn.GetPseudoLegalMoves(game).ToList();

		// Assert
		Assert.That(
			moves.Any(m => m.Kind == MoveKind.EnPassant), Is.False,
			"Should not include en passant captures when target square is not set");
	}

	[Test]
	public void Generate_NullGameParameter_Throws()
	{
		var g = new PawnPseudoValidMovesGenerator();
		Assert.Throws<ArgumentNullException>(() => g.Generate(null!, new PawnModel(PlayerColor.White)).ToList());
	}

	[Test]
	public void Generate_NullPiece_Throws()
	{
		var g = new PawnPseudoValidMovesGenerator();
		Assert.Throws<ArgumentNullException>(() => g.Generate(_standardGame, null!).ToList());
	}

	[Test]
	public void Generate_PawnOn7thRankOnEmptyBoard_EmitsPromotionMoves()
	{
		var game  = new GameModel(FenUtils.EmptyBoard);
		var board = game.Board;
		Assert.That(board.BoardPieces, Is.Empty);
		var pawn = board.CreatePieceAt("a7", PlayerColor.White, ChessPieceType.Pawn);
		Assert.That(board.BoardPieces,      Has.Count.EqualTo(1));
		Assert.That(board.PieceIndex,       Has.Count.EqualTo(1));
		Assert.That(board.GetPieceAt("a7"), Is.TypeOf<PawnModel>());

		var moves = pawn.GetPseudoLegalMoves(game).ToList();

		Assert.That(moves, Is.Not.Empty);

		TestContext.Out.WriteLine($"Pseudo moves count: {moves.Count}");
		foreach (var move in moves)
		{
			TestContext.Out.WriteLine(move);
			TestContext.Out.WriteLine($"Kind: {move.Kind}");
		}
	}

	[Test]
	public void Generate_PawnOn7thRankWithCapturablePieces_EmitsPromotionMoves()
	{
		var game  = new GameModel(FenUtils.EmptyBoard);
		var board = game.Board;
		Assert.That(board.BoardPieces, Is.Empty);
		var pawn = board.CreatePieceAt("b7", PlayerColor.White, ChessPieceType.Pawn);
		board.CreatePieceAt("a8", PlayerColor.Black, ChessPieceType.Rook);
		board.CreatePieceAt("c8", PlayerColor.Black, ChessPieceType.Rook);

		var moves = pawn.GetPseudoLegalMoves(game).ToList();

		Assert.That(moves, Is.Not.Empty);
		Assert.That(moves, Has.Count.EqualTo(12));
		TestContext.Out.WriteLine($"Pseudo moves count: {moves.Count}");
		foreach (var move in moves)
		{
			TestContext.Out.WriteLine(move);
			TestContext.Out.WriteLine($"Kind: {move.Kind}");
		}
	}

	[Test]
	public void Generate_PawnWithEnemyPiecesInDiagonals_GeneratesCaptures()
	{
		var game = new GameModel(FenUtils.EmptyBoard);
		var pawn = game.Board.CreatePieceAt("e4", PlayerColor.White, ChessPieceType.Pawn);
		game.Board.CreatePieceAt("d5", PlayerColor.Black, ChessPieceType.Pawn);
		game.Board.CreatePieceAt("f5", PlayerColor.Black, ChessPieceType.Pawn);

		var moves = pawn.GetPseudoLegalMoves(game).ToList();

		Assert.Multiple(
			() =>
			{
				Assert.That(
					moves.Count(m => m.Kind == MoveKind.Capture), Is.EqualTo(2), "Should generate two capture moves");

				Assert.That(moves.Any(m => m.To.Algebraic == "d5"), Is.True, "Should include capture to d5");
				Assert.That(moves.Any(m => m.To.Algebraic == "f5"), Is.True, "Should include capture to f5");
			});
	}

	[Test]
	public void Generate_StartingPawn_Returns2ValidMoves()
	{
		// Arrange
		var pawn = _standardGame.Board.GetPieceAt("c2");

		// Act
		var moves = pawn.GetPseudoLegalMoves(_standardGame).ToList();

		// Assert
		var expectedMoves = new[]
		{
			(Square: "c3", Kind: MoveKind.Normal),
			(Square: "c4", Kind: MoveKind.Normal)
		};

		Assert.Multiple(
			() =>
			{
				Assert.That(moves, Has.Count.EqualTo(2));

				for (var i = 0 ; i < expectedMoves.Length ; i++)
				{
					var (square, kind) = expectedMoves[i];
					Assert.That(moves[i].To.Algebraic, Is.EqualTo(square), $"Move {i + 1} should be to {square}");
					Assert.That(moves[i].Kind,         Is.EqualTo(kind),   $"Move {i + 1} should be a {kind} move");
				}
			});
	}

	[Test]
	public void Generate_StartingPositionPawns_NoCaptureMoves()
	{
		var pawn  = _standardGame.Board.GetPieceAt("e2");
		var moves = pawn.GetPseudoLegalMoves(_standardGame).ToList();

		Assert.That(
			moves.Any(m => m.Kind == MoveKind.Capture), Is.False,
			"Pawns in starting position should not have any capture moves");
	}

	[Test]
	public void Generate_WhenPawnWithBlockingOpponentPiece_DoesNotGenerateMoves()
	{
		// Arrange
		var pawn = _standardGame.Board.GetPieceAt("e2");
		_standardGame.Board.CreatePieceAt("e3", PlayerColor.Black, ChessPieceType.Pawn);

		// Act
		var moves = pawn.GetPseudoLegalMoves(_standardGame).ToList();

		// Assert
		Assert.That(moves, Is.Empty, "Pawn should not have any valid moves when blocked by opponent piece");
	}

	[Test]
	public void Generate_WhitePawnCanCaptureBlackPawnEnPassantFromLeft()
	{
		// Arrange
		var game      = new GameModel(FenUtils.EmptyBoard);
		var whitePawn = game.Board.CreatePieceAt("e5", PlayerColor.White, ChessPieceType.Pawn);
		var blackPawn = game.Board.CreatePieceAt("f5", PlayerColor.Black, ChessPieceType.Pawn);
		game.Board.SetEnPassantTargetSquare(game.Board.GetSquareAt("f6"));

		// Act
		var moves = whitePawn.GetPseudoLegalMoves(game).ToList();

		// Assert
		Assert.Multiple(
			() =>
			{
				Assert.That(moves.Any(m => m.Kind == MoveKind.EnPassant), Is.True, "Should include en passant capture");
				Assert.That(moves.Any(m => m.To.Algebraic == "f6"), Is.True, "Should include move to f6 square");
			});
	}

	[Test]
	public void Generate_WhitePawnCanCaptureBlackPawnEnPassantFromRight()
	{
		// Arrange
		var game      = new GameModel(FenUtils.EmptyBoard);
		var whitePawn = game.Board.CreatePieceAt("c5", PlayerColor.White, ChessPieceType.Pawn);
		var blackPawn = game.Board.CreatePieceAt("b5", PlayerColor.Black, ChessPieceType.Pawn);
		game.Board.SetEnPassantTargetSquare(game.Board.GetSquareAt("b6"));

		// Act
		var moves = whitePawn.GetPseudoLegalMoves(game).ToList();

		// Assert
		Assert.Multiple(
			() =>
			{
				Assert.That(moves.Any(m => m.Kind == MoveKind.EnPassant), Is.True, "Should include en passant capture");
				Assert.That(moves.Any(m => m.To.Algebraic == "b6"), Is.True, "Should include move to b6 square");
			});
	}

	[Test]
	public void Generate_WithNonPawnPiece_Throws()
	{
		var g    = new PawnPseudoValidMovesGenerator();
		var rook = _standardGame.Board.GetPieceAt("a1");
		Assert.Throws<ArgumentException>(() => g.Generate(_standardGame, rook).ToList());
	}

#endregion
}
