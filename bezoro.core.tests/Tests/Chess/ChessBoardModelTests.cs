using System;
using Bezoro.Core.Chess;
using Bezoro.Core.Chess.Utils;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Chess
{
	[TestFixture]
	public class ChessBoardModelTests
	{
		[Test]
		public void Constructor_CustomFen_InitializesBoardCorrectly()
		{
			// Arrange: White King on e1, Black King on e8, White Pawn on a2
			var customFen = FenUtility.ParseFen("4k3/8/8/8/8/8/P7/4K3 w - - 0 1");

			// Act
			var board = new ChessBoardModel(8, 8, customFen);

			// Assert
			Assert.That(board.Pieces.Length, Is.EqualTo(3));

			var e1Piece = BoardUtils.GetPieceAt(board, "e1");
			Assert.That(e1Piece,       Is.Not.Null);
			Assert.That(e1Piece.Type,  Is.EqualTo(ChessPieceType.King));
			Assert.That(e1Piece.Color, Is.EqualTo(PlayerColor.White));

			var e8Piece = BoardUtils.GetPieceAt(board, "e8");
			Assert.That(e8Piece,       Is.Not.Null);
			Assert.That(e8Piece.Type,  Is.EqualTo(ChessPieceType.King));
			Assert.That(e8Piece.Color, Is.EqualTo(PlayerColor.Black));

			var a2Piece = BoardUtils.GetPieceAt(board, "a2");
			Assert.That(a2Piece,       Is.Not.Null);
			Assert.That(a2Piece.Type,  Is.EqualTo(ChessPieceType.Pawn));
			Assert.That(a2Piece.Color, Is.EqualTo(PlayerColor.White));

			Assert.That(BoardUtils.GetPieceAt(board, "d4"), Is.Null, "An empty square should be null.");
		}

		[Test]
		public void Constructor_EmptyBoardFen_InitializesEmptyBoard()
		{
			// Arrange
			var emptyFen = FenUtility.ParseFen("8/8/8/8/8/8/8/8 w - - 0 1");

			// Act
			var board = new ChessBoardModel(8, 8, emptyFen);

			// Assert
			Assert.That(board.Pieces.Length, Is.EqualTo(0), "There should be no pieces on an empty board.");
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
			Assert.That(board.Pieces.Length, Is.EqualTo(4));

			// From FEN Rank 8 ("rnbqkbnr") on board rank 2 (index 1):
			// 'r' (a8 FEN) -> a2 on board (Squares[0,1])
			var a2_blackRook = BoardUtils.GetPieceAt(board, "a2");
			Assert.That(a2_blackRook,       Is.Not.Null, "Piece at a2 should be Black Rook.");
			Assert.That(a2_blackRook.Type,  Is.EqualTo(ChessPieceType.Rook));
			Assert.That(a2_blackRook.Color, Is.EqualTo(PlayerColor.Black));

			// 'n' (b8 FEN) -> b2 on board (Squares[1,1])
			var b2_blackKnight = BoardUtils.GetPieceAt(board, "b2");
			Assert.That(b2_blackKnight,       Is.Not.Null, "Piece at b2 should be Black Knight.");
			Assert.That(b2_blackKnight.Type,  Is.EqualTo(ChessPieceType.Knight));
			Assert.That(b2_blackKnight.Color, Is.EqualTo(PlayerColor.Black));

			// From FEN Rank 7 ("pppppppp") on board rank 1 (index 0):
			// 'p' (a7 FEN) -> a1 on board (Squares[0,0])
			var a1_blackPawn = BoardUtils.GetPieceAt(board, "a1");
			Assert.That(a1_blackPawn,       Is.Not.Null, "Piece at a1 should be Black Pawn.");
			Assert.That(a1_blackPawn.Type,  Is.EqualTo(ChessPieceType.Pawn));
			Assert.That(a1_blackPawn.Color, Is.EqualTo(PlayerColor.Black));

			// 'p' (b7 FEN) -> b1 on board (Squares[1,0])
			var b1_blackPawn = BoardUtils.GetPieceAt(board, "b1");
			Assert.That(b1_blackPawn,       Is.Not.Null, "Piece at b1 should be Black Pawn.");
			Assert.That(b1_blackPawn.Type,  Is.EqualTo(ChessPieceType.Pawn));
			Assert.That(b1_blackPawn.Color, Is.EqualTo(PlayerColor.Black));
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
			Assert.That(board.Pieces.Length, Is.EqualTo(2));

			// FEN rank 2 ("r1") maps to board's highest rank (rank 3, index 2)
			// 'r' at file 'a' -> board.Squares[0,2] (algebraic a3)
			var a3Piece = BoardUtils.GetPieceAt(board, "a3");
			Assert.That(a3Piece,          Is.Not.Null, "Black Rook (from FEN r1) should be at a3 on 3x3 board.");
			Assert.That(a3Piece.Type,     Is.EqualTo(ChessPieceType.Rook));
			Assert.That(a3Piece.Color,    Is.EqualTo(PlayerColor.Black));
			Assert.That(a3Piece.Position, Is.EqualTo(new ChessPosition(0, 2)));

			// FEN rank 1 ("1P") maps to board's next rank down (rank 2, index 1)
			// 'P' at file 'b' -> board.Squares[1,1] (algebraic b2)
			var b2Piece = BoardUtils.GetPieceAt(board, "b2");
			Assert.That(b2Piece,          Is.Not.Null, "White Pawn (from FEN 1P) should be at b2 on 3x3 board.");
			Assert.That(b2Piece.Type,     Is.EqualTo(ChessPieceType.Pawn));
			Assert.That(b2Piece.Color,    Is.EqualTo(PlayerColor.White));
			Assert.That(b2Piece.Position, Is.EqualTo(new ChessPosition(1, 1)));

			// Check other squares are empty
			Assert.That(BoardUtils.GetPieceAt(board, "a1"), Is.Null); // sq[0,0]
			Assert.That(BoardUtils.GetPieceAt(board, "b1"), Is.Null); // sq[1,0]
			Assert.That(BoardUtils.GetPieceAt(board, "c1"), Is.Null); // sq[2,0]
			Assert.That(BoardUtils.GetPieceAt(board, "a2"), Is.Null); // sq[0,1] (b2 is Pawn)
			Assert.That(BoardUtils.GetPieceAt(board, "c2"), Is.Null); // sq[2,1]
			// sq[0,2] is Rook, sq[1,2] should be empty
			Assert.That(BoardUtils.GetPieceAt(board, "b3"), Is.Null); // sq[1,2]
			Assert.That(BoardUtils.GetPieceAt(board, "c3"), Is.Null); // sq[2,2]
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
			Assert.That(board.Pieces, Has.Length.EqualTo(32), "Should be 32 pieces on a standard board.");

			// White pieces (Rank 1: index 0, Rank 2: index 1)
			var a1Piece = BoardUtils.GetPieceAt(board, "a1");
			Assert.That(a1Piece,          Is.Not.Null, "Piece at a1 (White Rook) should exist.");
			Assert.That(a1Piece.Type,     Is.EqualTo(ChessPieceType.Rook));
			Assert.That(a1Piece.Color,    Is.EqualTo(PlayerColor.White));
			Assert.That(a1Piece.Square,   Is.SameAs(board.Squares[0, 0]));
			Assert.That(a1Piece.Position, Is.EqualTo(new ChessPosition(0, 0)));

			var e1Piece = BoardUtils.GetPieceAt(board, "e1");
			Assert.That(e1Piece,                 Is.Not.Null, "Piece at e1 (White King) should exist.");
			Assert.That(e1Piece.Type,            Is.EqualTo(ChessPieceType.King));
			Assert.That(e1Piece.Color,           Is.EqualTo(PlayerColor.White));
			Assert.That(e1Piece.Square.Position, Is.EqualTo(new ChessPosition(4, 0)));

			var h2Piece = BoardUtils.GetPieceAt(board, "h2");
			Assert.That(h2Piece,                 Is.Not.Null, "Piece at h2 (White Pawn) should exist.");
			Assert.That(h2Piece.Type,            Is.EqualTo(ChessPieceType.Pawn));
			Assert.That(h2Piece.Color,           Is.EqualTo(PlayerColor.White));
			Assert.That(h2Piece.Square.Position, Is.EqualTo(new ChessPosition(7, 1)));

			// Black pieces (Rank 8: index 7, Rank 7: index 6)
			var a8Piece = BoardUtils.GetPieceAt(board, "a8");
			Assert.That(a8Piece,                 Is.Not.Null, "Piece at a8 (Black Rook) should exist.");
			Assert.That(a8Piece.Type,            Is.EqualTo(ChessPieceType.Rook));
			Assert.That(a8Piece.Color,           Is.EqualTo(PlayerColor.Black));
			Assert.That(a8Piece.Square.Position, Is.EqualTo(new ChessPosition(0, 7)));

			var e8Piece = BoardUtils.GetPieceAt(board, "e8");
			Assert.That(e8Piece,       Is.Not.Null, "Piece at e8 (Black King) should exist.");
			Assert.That(e8Piece.Type,  Is.EqualTo(ChessPieceType.King));
			Assert.That(e8Piece.Color, Is.EqualTo(PlayerColor.Black));

			var h7Piece = BoardUtils.GetPieceAt(board, "h7");
			Assert.That(h7Piece,       Is.Not.Null, "Piece at h7 (Black Pawn) should exist.");
			Assert.That(h7Piece.Type,  Is.EqualTo(ChessPieceType.Pawn));
			Assert.That(h7Piece.Color, Is.EqualTo(PlayerColor.Black));

			// Check an empty square in the middle
			var e4Piece = BoardUtils.GetPieceAt(board, "e4");
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

			Assert.That(board.Pieces, Is.Not.Null);

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
	}
}
