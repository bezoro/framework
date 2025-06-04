using Bezoro.Core.Chess;
using Bezoro.Core.Chess.Interfaces;
using Moq;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Chess
{
	[TestFixture]
	public class BoardSquareModelTests
	{
	#region Test Methods

		// --------------------------------------------------------------------
		//  ClearPiece
		// --------------------------------------------------------------------
		[Test]
		public void ClearPiece_AlwaysClearsPieceAndUpdatesFlags()
		{
			// arrange
			var piece  = CreatePiece().Object;
			var square = new BoardSquareModel(4, 4);
			square.SetPiece(piece);

			// act
			square.ClearPiece();

			// assert
			Assert.Multiple(
				() =>
				{
					Assert.That(square.Piece,      Is.Null);
					Assert.That(square.IsEmpty,    Is.True);
					Assert.That(square.IsOccupied, Is.False);
				});
		}

		// --------------------------------------------------------------------
		//  Ctors
		// --------------------------------------------------------------------
		[Test]
		public void Ctor_WithPositionAndPiece_InitialisesAllMembers()
		{
			// arrange
			var position = new BoardPosition(3, 5);
			var piece    = CreatePiece().Object;

			// act
			var square = new BoardSquareModel(position, piece);

			// assert
			Assert.Multiple(
				() =>
				{
					Assert.That(square.Position,   Is.EqualTo(position), "Position should be stored as-is");
					Assert.That(square.Piece,      Is.SameAs(piece),     "Piece should be stored");
					Assert.That(square.IsOccupied, Is.True,              "Square must report occupied");
					Assert.That(square.IsEmpty,    Is.False,             "Square must not report empty");
				});
		}

		[Test]
		public void Ctor_WithRowCol_InitialisesPosition_AndStartsEmpty()
		{
			// arrange / act
			var square = new BoardSquareModel(2, 6);

			// assert
			Assert.Multiple(
				() =>
				{
					Assert.That(square.Position.Row,    Is.EqualTo(6));
					Assert.That(square.Position.Column, Is.EqualTo(2));
					Assert.That(square.Piece,           Is.Null);
					Assert.That(square.IsEmpty,         Is.True);
					Assert.That(square.IsOccupied,      Is.False);
				});
		}

		// --------------------------------------------------------------------
		//  RemovePiece
		// --------------------------------------------------------------------
		[Test]
		public void RemovePiece_WhenDifferentPiece_DoesNothing()
		{
			// arrange
			var pieceOnSquare = CreatePiece("OnSquare").Object;
			var pieceToRemove = CreatePiece("Other").Object;
			var square        = new BoardSquareModel(1, 1);
			square.SetPiece(pieceOnSquare);

			// act
			square.RemovePiece(pieceToRemove);

			// assert
			Assert.Multiple(
				() =>
				{
					Assert.That(square.Piece,      Is.SameAs(pieceOnSquare));
					Assert.That(square.IsOccupied, Is.True);
				});
		}

		[Test]
		public void RemovePiece_WhenSamePiece_RemovesPieceAndUpdatesFlags()
		{
			// arrange
			var piece  = CreatePiece().Object;
			var square = new BoardSquareModel(2, 2);
			square.SetPiece(piece);

			// act
			square.RemovePiece(piece);

			// assert
			Assert.Multiple(
				() =>
				{
					Assert.That(square.Piece,   Is.Null);
					Assert.That(square.IsEmpty, Is.True);
				});
		}

		// --------------------------------------------------------------------
		//  SetPiece
		// --------------------------------------------------------------------
		[Test]
		public void SetPiece_SetsPieceAndUpdatesFlags()
		{
			// arrange
			var square = new BoardSquareModel(0, 0);
			var piece  = CreatePiece().Object;

			// act
			square.SetPiece(piece);

			// assert
			Assert.Multiple(
				() =>
				{
					Assert.That(square.Piece,      Is.SameAs(piece));
					Assert.That(square.IsOccupied, Is.True);
					Assert.That(square.IsEmpty,    Is.False);
				});
		}

	#endregion

	#region Helper Methods/Other Members

		// --------------------------------------------------------------------
		//  Helpers
		// --------------------------------------------------------------------
		private static Mock<IChessPieceModel> CreatePiece(string name = "Piece") =>
			new(MockBehavior.Strict) { DefaultValue = DefaultValue.Mock };

	#endregion
	}
}
