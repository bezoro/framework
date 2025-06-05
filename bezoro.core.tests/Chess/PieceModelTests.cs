using System.Collections.Generic;
using Bezoro.Core.Chess.Common.Enums;
using Bezoro.Core.Chess.Game.Models;
using Bezoro.Core.Chess.Moves.Models;
using Bezoro.Core.Chess.Pieces.Models;
using Moq;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Chess
{
	[TestFixture]
	public class PieceModelTests
	{
	#region Test Methods

		[Test]
		public void GetPseudoLegalMoves_DelegatesToGenerator()
		{
			// Arrange
			var game  = new GameModel();
			var moves = new List<Move> { new(new(0, 0), new(0, 1), PlayerColor.White, ChessPieceType.Pawn) };

			var genMock = new Mock<IPseudoMoveGenerator>();
			genMock
				.Setup(g => g.Generate(game, It.IsAny<PieceModel>()))
				.Returns(moves);

			var piece = new DummyPiece(PlayerColor.White, genMock.Object);

			// Act
			var result = piece.GetPseudoLegalMoves(game);

			// Assert
			Assert.That(result, Is.SameAs(moves)); // returns what generator returned
			genMock.Verify(g => g.Generate(game, piece), Times.Once);
		}

		[Test]
		public void MarkMoved_And_ResetMoved_ToggleFlag()
		{
			var piece = new DummyPiece(PlayerColor.White, Mock.Of<IPseudoMoveGenerator>());

			Assert.That(piece.HasMoved, Is.False, "initial");
			piece.MarkMoved();
			Assert.That(piece.HasMoved, Is.True, "after MarkMoved");
			piece.ResetMoved();
			Assert.That(piece.HasMoved, Is.False, "after ResetMoved");
		}

		[TestCase(PlayerColor.White, PlayerColor.Black)]
		[TestCase(PlayerColor.Black, PlayerColor.White)]
		public void Opposite_ReturnsCorrectColor(PlayerColor input, PlayerColor expected)
		{
			var piece = new DummyPiece(input, Mock.Of<IPseudoMoveGenerator>());
			Assert.That(piece.Opposite, Is.EqualTo(expected));
		}

	#endregion

	#region Helper Methods/Other Members

		// Concrete helper class just for testing the abstract base
		private sealed class DummyPiece : PieceModel
		{
			public DummyPiece(PlayerColor color, IPseudoMoveGenerator gen)
				: base(color, gen) { }
		}

	#endregion
	}
}
