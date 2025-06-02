using Bezoro.Core.Chess;
using Bezoro.Core.Chess.Utils;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Chess
{
	[TestFixture]
	public class MovePieceCommandTests
	{
		private MovePieceCommand          _command;
		private TestChessBoardSquareModel _fromSquare;

		private TestChessPieceModel       _piece;
		private TestChessBoardSquareModel _toSquare;

	#region Setup/Teardown Methods

		[SetUp]
		public void SetUp()
		{
			_piece      = new(ChessPieceType.Rook, PlayerColor.Black);
			_fromSquare = new(new(3, 3)); // e.g., d4
			_toSquare   = new(new(3, 5)); // e.g., d6

			// Initial state: piece is on _fromSquare
			// Using the extension method for setup consistency
			_piece.SetAtSquare(_fromSquare);

			// Command is created when piece is at _fromSquare, targeting _toSquare
			_command = new(_piece, _toSquare);
		}

	#endregion

	#region Test Methods

		[Test]
		public void Undo_AfterPieceMovedToTargetSquare_MovesPieceBackToOriginalSquare()
		{
			// Arrange
			// Simulate the piece having been moved to the target square (_toSquare)
			_piece.MoveTo(_toSquare); // This uses the extension method

			// Pre-Undo state verification (sanity check)
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
			_piece.MoveTo(_toSquare); // Simulate execution of the command

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

			public bool              IsEmpty                  => Piece == null;
			public bool              IsOccupied               => Piece != null;
			public ChessPosition     Position                 { get; }
			public bool              IsHighlightedAsValidMove { get; set; }
			public bool              IsSelected               { get; set; }
			public IChessPieceModel? Piece                    { get; set; }

		#region Interface Implementations

			public bool TryRemovePiece(IChessPieceModel pieceToRemove)
			{
				if (Piece == pieceToRemove)
				{
					Piece = null;
					return true;
				}

				return false;
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

			public bool                    IsCaptured { get; set; }
			public bool                    IsSelected { get; set; }
			public ChessPieceType          Type       { get; }
			public ChessPosition           Position   { get; set; }
			public IChessBoardSquareModel? Square     { get; set; }
			public PlayerColor             Color      { get; }
		}

	#endregion
	}
}
