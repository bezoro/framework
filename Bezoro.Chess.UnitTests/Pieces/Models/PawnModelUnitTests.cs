using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board.Models;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Pieces.Models;
using Moq;

namespace Bezoro.Chess.UnitTests.Pieces.Models;

[TestFixture]
[TestOf(typeof(PawnModel))]
public class PawnModelUnitTests
{
#region Test Methods

	[Test]
	public void EnPassantFlags_AreSetAndClearedCorrectly()
	{
		var pawn = new PawnModel(PlayerColor.White);

		Assert.Multiple(
			() =>
			{
				Assert.That(pawn.CanBeCapturedEnPassant, Is.False);
				Assert.That(pawn.JustAdvancedTwoSquares, Is.False);
			});

		pawn.SetEnPassantCapturable(true);
		pawn.SetJustAdvancedTwoSquares(true);

		Assert.Multiple(
			() =>
			{
				Assert.That(pawn.CanBeCapturedEnPassant, Is.True);
				Assert.That(pawn.JustAdvancedTwoSquares, Is.True);
			});

		pawn.ResetMoved(); // should clear both flags as well
		Assert.Multiple(
			() =>
			{
				Assert.That(pawn.CanBeCapturedEnPassant, Is.False);
				Assert.That(pawn.JustAdvancedTwoSquares, Is.False);
			});
	}

	[Test]
	public void PromoteTo_None_Throws()
	{
		var pawn   = new PawnModel(PlayerColor.White);
		var square = new Mock<BoardSquareModel>( /* ctor args if any */) { CallBase = true };

		Assert.Throws<ArgumentException>(
			() =>
				pawn.PromoteTo(square.Object, PromotionPieceType.None));
	}

	[TestCase(PlayerColor.White, 1)]
	[TestCase(PlayerColor.Black, -1)]
	public void Direction_IsSignedAccordingToColor(PlayerColor color, int expected)
	{
		var pawn = new PawnModel(color);
		Assert.That(pawn.Direction, Is.EqualTo(expected));
	}

	[TestCase(PromotionPieceType.Queen,  typeof(QueenModel))]
	[TestCase(PromotionPieceType.Rook,   typeof(RookModel))]
	[TestCase(PromotionPieceType.Bishop, typeof(BishopModel))]
	[TestCase(PromotionPieceType.Knight, typeof(KnightModel))]
	public void PromoteTo_ReplacesPieceWithCorrectType(PromotionPieceType choice, Type expectedClass)
	{
		var pawn   = new PawnModel(PlayerColor.Black);
		var square = new Mock<IChessBoardSquareModel>();

		// Act
		pawn.PromoteTo(square.Object, choice);

		// Assert
		square.Verify(
			sq => sq.SetPiece(
				It.Is<PieceModel>(
					p => p.GetType() == expectedClass && p.Color == pawn.Color)), Times.Once);
	}

#endregion
}
