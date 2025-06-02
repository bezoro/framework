using System;
using Bezoro.Core.Chess;
using Bezoro.Core.Chess.Utils;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Chess
{
	[TestFixture]
	public class ChessBoardModelTests
	{
	#region Test Methods

		[Test]
		public void IsValidPosition_ReturnsTrue_WhenPositionIsInsideOfTheBoard()
		{
			// Arrange
			var board = new ChessBoardModel(8, 8, FenUtility.EmptyBoard);

			// Act & Assert
			Assert.That(board.IsValidPosition("a1"), Is.True);
			Assert.That(board.IsValidPosition("a8"), Is.True);
			Assert.That(board.IsValidPosition("h1"), Is.True);
			Assert.That(board.IsValidPosition("h8"), Is.True);
			Assert.That(board.IsValidPosition("b2"), Is.True);
			Assert.That(board.IsValidPosition("c3"), Is.True);
		}

		[Test]
		public void IsValidPosition_Throws_WhenPositionIsOutsideOfTheBoard()
		{
			// Arrange
			var board = new ChessBoardModel(8, 8, FenUtility.EmptyBoard);

			// Act & Assert
			Assert.That(() => board.IsValidPosition("a0"), Throws.Exception);
			Assert.That(() => board.IsValidPosition("a9"), Throws.Exception);
			Assert.That(() => board.IsValidPosition("i1"), Throws.Exception);
			Assert.That(() => board.IsValidPosition("i9"), Throws.Exception);
			Assert.That(() => board.IsValidPosition("b0"), Throws.Exception);
			Assert.That(() => board.IsValidPosition("c9"), Throws.Exception);
		}

	#endregion

	#region Helper Methods/Other Members

		private static void AssertPieceExistsAt(
			IChessPieceModel piece,
			IChessBoardSquareModel square,
			ChessPieceType pieceType,
			PlayerColor color,
			ChessPosition position)
		{
			Assert.That(piece,          Is.Not.Null, $"Piece at {position.Algebraic} (White Rook) should exist.");
			Assert.That(piece.Square,   Is.SameAs(square));
			Assert.That(piece.Type,     Is.EqualTo(pieceType));
			Assert.That(piece.Color,    Is.EqualTo(color));
			Assert.That(piece.Position, Is.EqualTo(position));
		}

	#endregion

	#region CreatePieceAt Tests

		[Test]
		public void CreatePieceAt_ValidPosition_CreatesPiece()
		{
			// Arrange
			var board = new ChessBoardModel(8, 8, FenUtility.EmptyBoard);

			// Act
			board.CreatePieceAt("a1", PlayerColor.White, ChessPieceType.Pawn);

			Assert.Multiple(
				() =>
				{
					// Assert
					Assert.That(board.BoardPieces,            Has.Count.EqualTo(1));
					Assert.That(board.GetPieceAt("a1"),       Is.Not.Null);
					Assert.That(board.GetPieceAt("a1").Type,  Is.EqualTo(ChessPieceType.Pawn));
					Assert.That(board.GetPieceAt("a1").Color, Is.EqualTo(PlayerColor.White));
				});
		}

		[Test]
		public void CreatePieceAt_Throws_WhenParametersAreInvalid()
		{
			// Arrange
			var board = new ChessBoardModel(8, 8, FenUtility.EmptyBoard);

			// Act & Assert
			Assert.That(() => board.CreatePieceAt("a1",  PlayerColor.None,  ChessPieceType.Pawn), Throws.Exception);
			Assert.That(() => board.CreatePieceAt("a1",  PlayerColor.White, ChessPieceType.None), Throws.Exception);
			Assert.That(() => board.CreatePieceAt("xxx", PlayerColor.White, ChessPieceType.Pawn), Throws.Exception);
			Assert.That(() => board.CreatePieceAt("a1",  PlayerColor.White, ChessPieceType.Pawn), Throws.Nothing);
		}

	#endregion

	#region Constructor Tests

		[Test]
		public void Constructor_CustomFen_InitializesBoardCorrectly()
		{
			// Arrange: White King on e1, Black King on e8, White Pawn on a2
			var customFen = FenUtility.ParseFen("4k3/8/8/8/8/8/P7/4K3 w - - 0 1");

			// Act
			var board = new ChessBoardModel(8, 8, customFen);

			// Assert
			Assert.That(board.BoardPieces, Has.Count.EqualTo(3));

			var e1Piece = board.GetPieceAt("e1");
			Assert.That(e1Piece,       Is.Not.Null);
			Assert.That(e1Piece.Type,  Is.EqualTo(ChessPieceType.King));
			Assert.That(e1Piece.Color, Is.EqualTo(PlayerColor.White));

			var e8Piece = board.GetPieceAt("e8");
			Assert.That(e8Piece,       Is.Not.Null);
			Assert.That(e8Piece.Type,  Is.EqualTo(ChessPieceType.King));
			Assert.That(e8Piece.Color, Is.EqualTo(PlayerColor.Black));

			var a2Piece = board.GetPieceAt("a2");
			Assert.That(a2Piece,       Is.Not.Null);
			Assert.That(a2Piece.Type,  Is.EqualTo(ChessPieceType.Pawn));
			Assert.That(a2Piece.Color, Is.EqualTo(PlayerColor.White));

			Assert.That(board.GetPieceAt("d4"), Is.Null, "An empty square should be null.");
		}

		[Test]
		public void Constructor_EmptyBoardFen_InitializesEmptyBoard()
		{
			// Arrange
			var emptyFen = FenUtility.ParseFen("8/8/8/8/8/8/8/8 w - - 0 1");

			// Act
			var board = new ChessBoardModel(8, 8, emptyFen);

			// Assert
			Assert.That(board.BoardPieces, Is.Empty, "There should be no pieces on an empty board.");
			for (var file = 0 ; file < board.Width ; file++)
			{
				for (var rank = 0 ; rank < board.Height ; rank++)
				{
					Assert.That(
						board.Squares[file, rank].Piece, Is.Null, $"Square at [{file},{rank}] should be empty.");

					Assert.That(
						board.Squares[file, rank].IsEmpty, Is.True,
						$"Square at [{file},{rank}] should report IsEmpty = true.");
				}
			}
		}

		[Test]
		public void Constructor_FenForLargerBoardThanActualDimensions_InitializesOnlyFittingPiecesFromTopRanks()
		{
			// Arrange: Standard 8x8 FEN
			var standardFen = FenUtility.StandardBoard;

			// Act: Create a 2x2 board
			var board = new ChessBoardModel(2, 2, standardFen); // Board is 2 files (a,b) x 2 ranks (1,2)

			// Assert
			// Only pieces from the FEN's top ranks that fit onto the 2x2 board should be placed.
			// FEN rank 8 ("rnbqkbnr") maps to board rank 2 (index 1).
			// FEN rank 7 ("pppppppp") maps to board rank 1 (index 0).
			// Other FEN ranks will result in rank index < 0 and be ignored.
			Assert.That(board.BoardPieces, Has.Count.EqualTo(4));

			// From FEN Rank 8 ("rnbqkbnr") on board rank 2 (index 1):
			// 'r' (a8 FEN) -> a2 on board (Squares[0,1])
			var a2BlackRook = board.GetPieceAt("a2");
			Assert.That(a2BlackRook,       Is.Not.Null, "Piece at a2 should be Black Rook.");
			Assert.That(a2BlackRook.Type,  Is.EqualTo(ChessPieceType.Rook));
			Assert.That(a2BlackRook.Color, Is.EqualTo(PlayerColor.Black));

			// 'n' (b8 FEN) -> b2 on board (Squares[1,1])
			var b2BlackKnight = board.GetPieceAt("b2");
			Assert.That(b2BlackKnight,       Is.Not.Null, "Piece at b2 should be Black Knight.");
			Assert.That(b2BlackKnight.Type,  Is.EqualTo(ChessPieceType.Knight));
			Assert.That(b2BlackKnight.Color, Is.EqualTo(PlayerColor.Black));

			// From FEN Rank 7 ("pppppppp") on board rank 1 (index 0):
			// 'p' (a7 FEN) -> a1 on board (Squares[0,0])
			var a1BlackPawn = board.GetPieceAt("a1");
			Assert.That(a1BlackPawn,       Is.Not.Null, "Piece at a1 should be Black Pawn.");
			Assert.That(a1BlackPawn.Type,  Is.EqualTo(ChessPieceType.Pawn));
			Assert.That(a1BlackPawn.Color, Is.EqualTo(PlayerColor.Black));

			// 'p' (b7 FEN) -> b1 on board (Squares[1,0])
			var b1BlackPawn = board.GetPieceAt("b1");
			Assert.That(b1BlackPawn,       Is.Not.Null, "Piece at b1 should be Black Pawn.");
			Assert.That(b1BlackPawn.Type,  Is.EqualTo(ChessPieceType.Pawn));
			Assert.That(b1BlackPawn.Color, Is.EqualTo(PlayerColor.Black));
		}

		[Test]
		public void Constructor_FenForSmallerBoardThanActualDimensions_InitializesPartiallyFromTopRanks()
		{
			// Arrange: FEN for a 2x2 board: black rook at a2 (FEN), white pawn at b1 (FEN)
			// FEN "r1/1P" means: rank 2 is 'r', empty; rank 1 is empty, 'P'.
			var smallFenData = FenUtility.ParseFen("r1/1P w - - 0 1");

			// Act: Create a 3x3 board using this 2-rank FEN
			var board = new ChessBoardModel(3, 3, smallFenData); // Board is 3 files (a,b,c) x 3 ranks (1,2,3)

			// Assert
			Assert.That(board.BoardPieces, Has.Count.EqualTo(2));

			// FEN rank 2 ("r1") maps to board's highest rank (rank 3, index 2)
			// 'r' at file 'a' -> board.Squares[0,2] (algebraic a3)
			var a3Piece = board.GetPieceAt("a3");
			Assert.That(a3Piece,          Is.Not.Null, "Black Rook (from FEN r1) should be at a3 on 3x3 board.");
			Assert.That(a3Piece.Type,     Is.EqualTo(ChessPieceType.Rook));
			Assert.That(a3Piece.Color,    Is.EqualTo(PlayerColor.Black));
			Assert.That(a3Piece.Position, Is.EqualTo(new ChessPosition(0, 2)));

			// FEN rank 1 ("1P") maps to board's next rank down (rank 2, index 1)
			// 'P' at file 'b' -> board.Squares[1,1] (algebraic b2)
			var b2Piece = board.GetPieceAt("b2");
			Assert.That(b2Piece,          Is.Not.Null, "White Pawn (from FEN 1P) should be at b2 on 3x3 board.");
			Assert.That(b2Piece.Type,     Is.EqualTo(ChessPieceType.Pawn));
			Assert.That(b2Piece.Color,    Is.EqualTo(PlayerColor.White));
			Assert.That(b2Piece.Position, Is.EqualTo(new ChessPosition(1, 1)));

			// Check other squares are empty
			Assert.That(board.GetPieceAt("a1"), Is.Null); // sq[0,0]
			Assert.That(board.GetPieceAt("b1"), Is.Null); // sq[1,0]
			Assert.That(board.GetPieceAt("c1"), Is.Null); // sq[2,0]
			Assert.That(board.GetPieceAt("a2"), Is.Null); // sq[0,1] (b2 is Pawn)
			Assert.That(board.GetPieceAt("c2"), Is.Null); // sq[2,1]
			// sq[0,2] is Rook, sq[1,2] should be empty
			Assert.That(board.GetPieceAt("b3"), Is.Null); // sq[1,2]
			Assert.That(board.GetPieceAt("c3"), Is.Null); // sq[2,2]
		}

		[Test]
		public void Constructor_InvalidHeight_ThrowsArgumentOutOfRangeException()
		{
			var fen = FenUtility.StandardBoard;
			Assert.Throws<ArgumentOutOfRangeException>(() => new ChessBoardModel(8, 0,  fen));
			Assert.Throws<ArgumentOutOfRangeException>(() => new ChessBoardModel(8, -5, fen));
		}

		[Test]
		public void Constructor_InvalidWidth_ThrowsArgumentOutOfRangeException()
		{
			var fen = FenUtility.StandardBoard;
			Assert.Throws<ArgumentOutOfRangeException>(() => new ChessBoardModel(0,  8, fen));
			Assert.Throws<ArgumentOutOfRangeException>(() => new ChessBoardModel(-1, 8, fen));
		}

		[Test]
		public void Constructor_StandardFen_InitializesBoardWithStandardSetup()
		{
			// Arrange
			var standardFen = FenUtility.StandardBoard; // "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"

			// Act
			var board = new ChessBoardModel(8, 8, standardFen);

			// Assert
			Assert.That(board.BoardPieces, Has.Count.EqualTo(32), "Should be 32 pieces on a standard board.");

			// White pieces (Rank 1: index 0, Rank 2: index 1)
			var a1Piece  = board.GetPieceAt("a1");
			var a1Square = board.GetSquareAt("a1");
			AssertPieceExistsAt(a1Piece, a1Square, ChessPieceType.Rook, PlayerColor.White, new(0, 0));

			var b1Piece  = board.GetPieceAt("b1");
			var b1Square = board.GetSquareAt("b1");
			AssertPieceExistsAt(b1Piece, b1Square, ChessPieceType.Knight, PlayerColor.White, new(1, 0));

			var c1Piece  = board.GetPieceAt("c1");
			var c1Square = board.GetSquareAt("c1");
			AssertPieceExistsAt(c1Piece, c1Square, ChessPieceType.Bishop, PlayerColor.White, new(2, 0));

			var d1Piece  = board.GetPieceAt("d1");
			var d1Square = board.GetSquareAt("d1");
			AssertPieceExistsAt(d1Piece, d1Square, ChessPieceType.Queen, PlayerColor.White, new(3, 0));

			var e1Piece  = board.GetPieceAt("e1");
			var e1Square = board.GetSquareAt("e1");
			AssertPieceExistsAt(e1Piece, e1Square, ChessPieceType.King, PlayerColor.White, new(4, 0));

			var f1Piece  = board.GetPieceAt("f1");
			var f1Square = board.GetSquareAt("f1");
			AssertPieceExistsAt(f1Piece, f1Square, ChessPieceType.Bishop, PlayerColor.White, new(5, 0));

			var g1Piece  = board.GetPieceAt("g1");
			var g1Square = board.GetSquareAt("g1");
			AssertPieceExistsAt(g1Piece, g1Square, ChessPieceType.Knight, PlayerColor.White, new(6, 0));

			var h1Piece  = board.GetPieceAt("h1");
			var h1Square = board.GetSquareAt("h1");
			AssertPieceExistsAt(h1Piece, h1Square, ChessPieceType.Rook, PlayerColor.White, new(7, 0));

			var a2Piece  = board.GetPieceAt("a2");
			var a2Square = board.GetSquareAt("a2");
			AssertPieceExistsAt(a2Piece, a2Square, ChessPieceType.Pawn, PlayerColor.White, new(0, 1));

			var b2Piece  = board.GetPieceAt("b2");
			var b2Square = board.GetSquareAt("b2");
			AssertPieceExistsAt(b2Piece, b2Square, ChessPieceType.Pawn, PlayerColor.White, new(1, 1));

			var c2Piece  = board.GetPieceAt("c2");
			var c2Square = board.GetSquareAt("c2");
			AssertPieceExistsAt(c2Piece, c2Square, ChessPieceType.Pawn, PlayerColor.White, new(2, 1));

			var d2Piece  = board.GetPieceAt("d2");
			var d2Square = board.GetSquareAt("d2");
			AssertPieceExistsAt(d2Piece, d2Square, ChessPieceType.Pawn, PlayerColor.White, new(3, 1));

			var e2Piece  = board.GetPieceAt("e2");
			var e2Square = board.GetSquareAt("e2");
			AssertPieceExistsAt(e2Piece, e2Square, ChessPieceType.Pawn, PlayerColor.White, new(4, 1));

			var f2Piece  = board.GetPieceAt("f2");
			var f2Square = board.GetSquareAt("f2");
			AssertPieceExistsAt(f2Piece, f2Square, ChessPieceType.Pawn, PlayerColor.White, new(5, 1));

			var g2Piece  = board.GetPieceAt("g2");
			var g2Square = board.GetSquareAt("g2");
			AssertPieceExistsAt(g2Piece, g2Square, ChessPieceType.Pawn, PlayerColor.White, new(6, 1));

			var h2Piece  = board.GetPieceAt("h2");
			var h2Square = board.GetSquareAt("h2");
			AssertPieceExistsAt(h2Piece, h2Square, ChessPieceType.Pawn, PlayerColor.White, new(7, 1));

			// Black pieces (Rank 8: index 7, Rank 7: index 6)

			var a8Piece  = board.GetPieceAt("a8");
			var a8Square = board.GetSquareAt("a8");
			AssertPieceExistsAt(a8Piece, a8Square, ChessPieceType.Rook, PlayerColor.Black, new(0, 7));

			var b8Piece  = board.GetPieceAt("b8");
			var b8Square = board.GetSquareAt("b8");
			AssertPieceExistsAt(b8Piece, b8Square, ChessPieceType.Knight, PlayerColor.Black, new(1, 7));

			var c8Piece  = board.GetPieceAt("c8");
			var c8Square = board.GetSquareAt("c8");
			AssertPieceExistsAt(c8Piece, c8Square, ChessPieceType.Bishop, PlayerColor.Black, new(2, 7));

			var d8Piece  = board.GetPieceAt("d8");
			var d8Square = board.GetSquareAt("d8");
			AssertPieceExistsAt(d8Piece, d8Square, ChessPieceType.Queen, PlayerColor.Black, new(3, 7));

			var e8Piece  = board.GetPieceAt("e8");
			var e8Square = board.GetSquareAt("e8");
			AssertPieceExistsAt(e8Piece, e8Square, ChessPieceType.King, PlayerColor.Black, new(4, 7));

			var f8Piece  = board.GetPieceAt("f8");
			var f8Square = board.GetSquareAt("f8");
			AssertPieceExistsAt(f8Piece, f8Square, ChessPieceType.Bishop, PlayerColor.Black, new(5, 7));

			var g8Piece  = board.GetPieceAt("g8");
			var g8Square = board.GetSquareAt("g8");
			AssertPieceExistsAt(g8Piece, g8Square, ChessPieceType.Knight, PlayerColor.Black, new(6, 7));

			var h8Piece  = board.GetPieceAt("h8");
			var h8Square = board.GetSquareAt("h8");
			AssertPieceExistsAt(h8Piece, h8Square, ChessPieceType.Rook, PlayerColor.Black, new(7, 7));

			var a7Piece  = board.GetPieceAt("a7");
			var a7Square = board.GetSquareAt("a7");
			AssertPieceExistsAt(a7Piece, a7Square, ChessPieceType.Pawn, PlayerColor.Black, new(0, 6));

			var b7Piece  = board.GetPieceAt("b7");
			var b7Square = board.GetSquareAt("b7");
			AssertPieceExistsAt(b7Piece, b7Square, ChessPieceType.Pawn, PlayerColor.Black, new(1, 6));

			var c7Piece  = board.GetPieceAt("c7");
			var c7Square = board.GetSquareAt("c7");
			AssertPieceExistsAt(c7Piece, c7Square, ChessPieceType.Pawn, PlayerColor.Black, new(2, 6));

			var d7Piece  = board.GetPieceAt("d7");
			var d7Square = board.GetSquareAt("d7");
			AssertPieceExistsAt(d7Piece, d7Square, ChessPieceType.Pawn, PlayerColor.Black, new(3, 6));

			var e7Piece  = board.GetPieceAt("e7");
			var e7Square = board.GetSquareAt("e7");
			AssertPieceExistsAt(e7Piece, e7Square, ChessPieceType.Pawn, PlayerColor.Black, new(4, 6));

			var f7Piece  = board.GetPieceAt("f7");
			var f7Square = board.GetSquareAt("f7");
			AssertPieceExistsAt(f7Piece, f7Square, ChessPieceType.Pawn, PlayerColor.Black, new(5, 6));

			var g7Piece  = board.GetPieceAt("g7");
			var g7Square = board.GetSquareAt("g7");
			AssertPieceExistsAt(g7Piece, g7Square, ChessPieceType.Pawn, PlayerColor.Black, new(6, 6));

			var h7Piece  = board.GetPieceAt("h7");
			var h7Square = board.GetSquareAt("h7");
			AssertPieceExistsAt(h7Piece, h7Square, ChessPieceType.Pawn, PlayerColor.Black, new(7, 6));

			// Check an empty square in the middle
			var e4Piece = board.GetPieceAt("e4");
			Assert.That(e4Piece,                     Is.Null, "Square e4 should be empty.");
			Assert.That(board.Squares[4, 3].IsEmpty, Is.True);
		}

		[Test]
		public void Constructor_ValidDimensions_InitializesBoardProperties()
		{
			// Arrange
			var fen = FenUtility.ParseFen("8/8/8/8/8/8/8/8 w - - 0 1"); // Empty board FEN

			// Act
			var board = new ChessBoardModel(8, 8, fen);

			// Assert
			Assert.That(board.Width,   Is.EqualTo(8));
			Assert.That(board.Height,  Is.EqualTo(8));
			Assert.That(board.Squares, Is.Not.Null);
			Assert.That(
				board.Squares.GetLength(0), Is.EqualTo(8), "Board Squares first dimension (width/files) is incorrect.");

			Assert.That(
				board.Squares.GetLength(1), Is.EqualTo(8),
				"Board Squares second dimension (height/ranks) is incorrect.");

			Assert.That(board.BoardPieces, Is.Not.Null);

			for (var f = 0 ; f < board.Width ; f++) // File index
			{
				for (var r = 0 ; r < board.Height ; r++) // Rank index
				{
					Assert.That(board.Squares[f, r], Is.Not.Null, $"Square at [{f},{r}] should be initialized.");
					Assert.That(
						board.Squares[f, r].Position, Is.EqualTo(new ChessPosition(f, r)),
						$"Square at [{f},{r}] has incorrect position.");
				}
			}
		}

	#endregion

	#region TryMovePiece Tests

		[Test]
		public void TryMovePiece_ToEmptySquare_UpdatesBoardAndReturnsTrue()
		{
			// Arrange
			var board     = new ChessBoardModel();  // Standard 8x8 board
			var whitePawn = board.GetPieceAt("e2"); // Position file 4, rank 1
			Assert.That(whitePawn, Is.Not.Null, "White pawn at e2 should exist.");
			var originalSquare = whitePawn.Square;
			var targetSquare   = board.GetSquareAt("e3"); // Get the square
			var command        = new MovePieceCommand(whitePawn, targetSquare);

			// Act
			var result = board.TryMovePiece(command);

			// Assert
			Assert.That(result,                Is.True, "Move should be successful.");
			Assert.That(originalSquare?.Piece, Is.Null, "Original square (e2) should be empty after move.");
			Assert.That(
				targetSquare.Piece, Is.EqualTo(whitePawn),
				"Target square (e3) should contain the moved pawn.");

			Assert.That(
				whitePawn.Position, Is.EqualTo(targetSquare.Position), "Pawn's position should be updated to e3.");

			Assert.That(
				whitePawn.Square, Is.EqualTo(targetSquare),
				"Pawn's square reference should be updated to e3.");

			Assert.That(
				board.GetPieceAt("e2"), Is.Null, "BoardUtils should not find piece at old position e2.");

			Assert.That(
				board.GetPieceAt("e3"), Is.EqualTo(whitePawn),
				"BoardUtils should find piece at new position e3.");
		}

		[Test]
		public void TryMovePiece_ValidMoveWithCapture_UpdatesBoardCapturesPieceAndReturnsTrue()
		{
			// Arrange
			// Setup: White Pawn e4, Black Pawn d5. White Pawn captures Black Pawn.
			// FEN: 8/8/8/3p4/4P3/8/8/8 w - - 0 1  (Black pawn at d5, White pawn at e4)
			var fen   = FenUtility.ParseFen("8/8/8/3p4/4P3/8/8/8 w - - 0 1");
			var board = new ChessBoardModel(8, 8, fen);

			var whitePawn          = board.GetPieceAt("e4"); // File 4, Rank 3
			var blackPawnToCapture = board.GetPieceAt("d5"); // File 3, Rank 4

			Assert.That(whitePawn,                     Is.Not.Null, "White pawn at e4 should exist.");
			Assert.That(blackPawnToCapture,            Is.Not.Null, "Black pawn at d5 should exist.");
			Assert.That(blackPawnToCapture.IsCaptured, Is.False,    "Black pawn should not be captured initially.");

			var originalSquare = whitePawn.Square;
			var targetSquare   = board.GetSquareAt("d5");
			var command        = new MovePieceCommand(whitePawn, targetSquare);

			// Act
			var result = board.TryMovePiece(command);

			// Assert
			Assert.That(result,                Is.True, "Move should be successful.");
			Assert.That(originalSquare?.Piece, Is.Null, "Original square (e4) should be empty after move.");
			Assert.That(
				board.Squares[targetSquare.Position.File, targetSquare.Position.Rank].Piece, Is.EqualTo(whitePawn),
				"Target square (d5) should contain the moved white pawn.");

			Assert.That(
				whitePawn.Position, Is.EqualTo(targetSquare.Position),
				"White pawn's position should be updated to d5.");

			Assert.That(
				whitePawn.Square, Is.EqualTo(board.Squares[targetSquare.Position.File, targetSquare.Position.Rank]),
				"White pawn's square reference should be updated to d5.");

			Assert.That(blackPawnToCapture.IsCaptured, Is.True, "Black pawn at d5 should be marked as captured.");
			Assert.That(
				board.GetPieceAt("e4"), Is.Null, "BoardUtils should not find piece at old position e4.");

			Assert.That(
				board.GetPieceAt("d5"), Is.EqualTo(whitePawn),
				"BoardUtils should find the white pawn at new position d5.");
		}

		[Test]
		public void TryMovePiece_CaptureOpponentPiece_UpdatesPieceListsAndFlags()
		{
			// Arrange
			// Minimal setup: White Pawn at e2, Black Pawn at d7. White moves e2-e4. Black moves d7-d5. White captures e4xd5.
			var fen = FenUtility.ParseFen(
				"rnbqkbnr/ppp1pppp/8/3p4/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2"); // After e4, d5

			var board = new ChessBoardModel(8, 8, fen);

			var whitePawnE4 = board.GetPieceAt("e4"); // White Pawn at e4
			var blackPawnD5 = board.GetPieceAt("d5"); // Black Pawn at d5 (target)

			Assert.That(whitePawnE4, Is.Not.Null, "White pawn at e4 should exist.");
			Assert.That(blackPawnD5, Is.Not.Null, "Black pawn at d5 should exist.");

			var initialBoardPiecesCount    = board.BoardPieces.Count;
			var initialCapturedPiecesCount = board.CapturedPieces.Count;

			var moveCommand = new MovePieceCommand(whitePawnE4, blackPawnD5.Square); // e4 captures d5

			// Act
			var moveResult = board.TryMovePiece(moveCommand);

			// Assert
			Assert.That(moveResult, Is.True, "Move (capture) should be successful.");

			// Check captured piece properties
			Assert.That(blackPawnD5.IsCaptured, Is.True, "Captured black pawn's IsCaptured flag should be true.");

			// Check lists
			Assert.That(
				board.BoardPieces, Does.Not.Contain(blackPawnD5),
				"Captured black pawn should be removed from BoardPieces.");

			Assert.That(
				board.CapturedPieces, Does.Contain(blackPawnD5),
				"Captured black pawn should be added to CapturedPieces.");

			Assert.That(
				board.BoardPieces.Count, Is.EqualTo(initialBoardPiecesCount - 1),
				"BoardPieces count should decrease by one.");

			Assert.That(
				board.CapturedPieces.Count, Is.EqualTo(initialCapturedPiecesCount + 1),
				"CapturedPieces count should increase by one.");

			// Check moving piece's new state
			Assert.That(
				whitePawnE4.Position, Is.EqualTo(board.GetSquareAt("d5").Position),
				"White pawn should now be at the capture square (d5).");

			Assert.That(
				board.GetPieceAt("d5"), Is.EqualTo(whitePawnE4),
				"Capture square should now contain the white pawn.");

			Assert.That(
				board.Squares[moveCommand.From.Position.File, moveCommand.From.Position.Rank].IsEmpty, Is.True,
				"Original square (e4) of white pawn should be empty.");
		}

	#endregion
	}
}
