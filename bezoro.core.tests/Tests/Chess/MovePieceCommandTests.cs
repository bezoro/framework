using System;
using Bezoro.Core.Chess;
using Bezoro.Core.Chess.Utils;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Chess
{
	[TestFixture]
	public class MovePieceCommandTests
	{
		private MovePieceCommand       _command;
		private IChessBoardSquareModel _fromSquare;

		private IChessPieceModel       _piece;
		private IChessBoardSquareModel _toSquare;

	#region Setup/Teardown Methods

		[SetUp]
		public void SetUp()
		{
			_piece      = new TestChessPieceModel(ChessPieceType.Rook, PlayerColor.Black);
			_fromSquare = new TestChessBoardSquareModel(new(3, 3)); // e.g., d4
			_toSquare   = new TestChessBoardSquareModel(new(3, 5)); // e.g., d6
			// Initial state: the piece is on _fromSquare
			// Using the extension method for setup consistency
			_piece.SetAtSquare(_fromSquare);
			// Command is created when piece is at _fromSquare, targeting _toSquare
			_command = new(_piece, _toSquare);
		}

	#endregion

	#region Test Methods

		[Test]
		public void Constructor_WhenPieceIsNotOnAnySquare_ThrowsArgumentException()
		{
			// Arrange
			// Create a piece that is not on any square for this specific test.
			var pieceWithoutSquare = new TestChessPieceModel(ChessPieceType.Knight);
			// Ensure its Square property is null (TestChessPieceModel constructor doesn't set Square).
			Assert.That(pieceWithoutSquare.Square, Is.Null, "Pre-condition: Test piece should not be on a square.");

			// Act & Assert
			var ex = Assert.Throws<ArgumentException>(() => new MovePieceCommand(pieceWithoutSquare, _toSquare));
			Assert.That(ex.ParamName, Is.EqualTo("pieceToMove"));
			Assert.That(ex.Message,   Contains.Substring("Piece must be on a board square."));
		}

		[Test]
		public void Constructor_WhenPieceToMoveIsNull_ThrowsArgumentNullException()
		{
			// Arrange
			IChessPieceModel nullPiece = null;

			// Act & Assert
			var ex = Assert.Throws<ArgumentNullException>(() => new MovePieceCommand(nullPiece, _toSquare));
			Assert.That(ex.ParamName, Is.EqualTo("pieceToMove"));
		}

		[Test]
		public void Constructor_WhenToSquareIsNull_ThrowsArgumentNullException()
		{
			// Arrange
			IChessBoardSquareModel nullToSquare = null;

			// Act & Assert
			var ex = Assert.Throws<ArgumentNullException>(() => new MovePieceCommand(_piece, nullToSquare));
			Assert.That(ex.ParamName, Is.EqualTo("to"));
		}

		[Test]
		public void Execute_MovesPieceToTargetSquare()
		{
			// Arrange
			// Pre-condition checks
			Assert.That(_piece.Square,     Is.EqualTo(_fromSquare), "Pre-condition: Piece should be on 'from' square.");
			Assert.That(_fromSquare.Piece, Is.EqualTo(_piece), "Pre-condition: 'From' square should hold the piece.");
			Assert.That(_toSquare.Piece,   Is.Null, "Pre-condition: 'To' square should be empty.");
			Assert.That(_command.Piece,    Is.EqualTo(_piece));
			Assert.That(_command.From,     Is.EqualTo(_fromSquare));
			Assert.That(_command.To,       Is.EqualTo(_toSquare));

			// Act
			_command.Execute();

			// Assert
			// Piece's state after Execute
			Assert.That(
				_piece.Square, Is.EqualTo(_toSquare), "Piece.Square should be updated to _toSquare after Execute.");

			Assert.That(
				_piece.Position, Is.EqualTo(_toSquare.Position),
				"Piece.Position should match _toSquare.Position after Execute.");

			// Squares' state after Execute
			Assert.That(_toSquare.Piece,   Is.EqualTo(_piece), "_toSquare should now contain the piece after Execute.");
			Assert.That(_fromSquare.Piece, Is.Null,            "_fromSquare should be empty after Execute.");
		}

		[Test]
		public void Undo_AfterPieceMovedToTargetSquare_MovesPieceBackToOriginalSquare()
		{
			// Arrange
			// Execute the command to move the piece from _fromSquare to _toSquare
			_command.Execute();

			// Pre-Undo state verification
			Assert.That(
				_piece.Square, Is.EqualTo(_toSquare), "Pre-condition: Piece should be on 'to' square before Undo.");

			Assert.That(
				_toSquare.Piece, Is.EqualTo(_piece), "Pre-condition: 'To' square should hold the piece before Undo.");

			Assert.That(_fromSquare.Piece, Is.Null, "Pre-condition: 'From' square should be empty before Undo.");

			// Act
			_command.Undo();

			// Assert
			Assert.That(
				_piece.Square, Is.EqualTo(_fromSquare), "Piece.Square should be restored to _fromSquare after Undo.");

			Assert.That(
				_piece.Position, Is.EqualTo(_fromSquare.Position),
				"Piece.Position should match _fromSquare.Position after Undo.");

			Assert.That(_fromSquare.Piece, Is.EqualTo(_piece), "_fromSquare.Piece should be the piece after Undo.");
			Assert.That(_toSquare.Piece,   Is.Null,            "_toSquare.Piece should be null after Undo.");
		}

		[Test]
		public void Undo_CalledMultipleTimes_PieceRemainsOnOriginalSquareAfterFirstUndo()
		{
			// Arrange
			_command.Execute();

			// Act
			_command.Undo(); // First undo
			_command.Undo(); // Second undo

			// Assert
			Assert.That(
				_piece.Square, Is.EqualTo(_fromSquare), "Piece.Square should be _fromSquare after multiple Undos.");

			Assert.That(
				_piece.Position, Is.EqualTo(_fromSquare.Position),
				"Piece.Position should match _fromSquare.Position after multiple Undos.");

			Assert.That(
				_fromSquare.Piece, Is.EqualTo(_piece), "_fromSquare.Piece should be the piece after multiple Undos.");

			Assert.That(_toSquare.Piece, Is.Null, "_toSquare.Piece should be null after multiple Undos.");
		}

		[Test]
		public void Undo_WhenPieceNotActuallyMovedFromOriginalSquare_EnsuresPieceIsOnOriginalSquare()
		{
			// Arrange
			// Piece is still on _fromSquare (as per SetUp).

			// Pre-Undo state verification
			Assert.That(_piece.Square,     Is.EqualTo(_fromSquare), "Pre-condition: Piece should be on 'from' square.");
			Assert.That(_fromSquare.Piece, Is.EqualTo(_piece), "Pre-condition: 'From' square should hold the piece.");
			Assert.That(_toSquare.Piece,   Is.Null, "Pre-condition: 'To' square should be empty.");

			// Act
			_command.Undo();

			// Assert
			Assert.That(_piece.Square, Is.EqualTo(_fromSquare), "Piece.Square should remain/be _fromSquare.");
			Assert.That(
				_piece.Position, Is.EqualTo(_fromSquare.Position), "Piece.Position should match _fromSquare.Position.");

			Assert.That(_fromSquare.Piece, Is.EqualTo(_piece), "_fromSquare.Piece should be the piece.");
			Assert.That(_toSquare.Piece, Is.Null, "_toSquare.Piece should remain null as piece was never moved there.");
		}

	#endregion

	#region Helper Methods/Other Members

		// Helper class for IChessBoardSquareModel
		private class TestChessBoardSquareModel : IChessBoardSquareModel
		{
			public TestChessBoardSquareModel(ChessPosition position)
			{
				Position = position;
			}

			public bool              IsEmpty => Piece == null;
			public bool              IsOccupied => Piece != null;
			public ChessPosition     Position { get; }
			public bool              IsHighlightedAsValidMove { get; set; }
			public bool              IsSelected { get; set; }
			public IChessPieceModel? Piece { get; set; } // Made public set for test setup flexibility

		#region Interface Implementations

			public bool TryRemovePiece(IChessPieceModel pieceToRemove)
			{
				if (Piece != pieceToRemove)
					return false;

				Piece = null;
				return true;
			}

		#endregion
		}

		// Helper class for IChessPieceModel
		private class TestChessPieceModel : IChessPieceModel
		{
			public TestChessPieceModel(ChessPieceType type = ChessPieceType.Pawn, PlayerColor color = PlayerColor.White)
			{
				Type  = type;
				Color = color;
			}

			public ChessPieceType Type  { get; }
			public PlayerColor    Color { get; }

			public bool                    IsCaptured { get; set; }
			public bool                    IsSelected { get; set; }
			public ChessPosition           Position   { get; set; }
			public IChessBoardSquareModel? Square     { get; set; }
		}

	#endregion
	}
}
