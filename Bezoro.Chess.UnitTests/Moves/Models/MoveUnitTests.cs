using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Moves.Models;

namespace Bezoro.Chess.UnitTests.Moves.Models;

[TestFixture]
[TestOf(typeof(Move))]
public class MoveUnitTests
{
	private static readonly BoardPosition A7 = new(0, 6); // a7
	private static readonly BoardPosition A8 = new(0, 7); // a8
	private static readonly BoardPosition E2 = new(4, 1); // e2
	private static readonly BoardPosition E4 = new(4, 3); // e4

#region Test Methods

#region Sad paths

	[Test]
	public void EqualsObject_ReturnsFalseForDifferentMove()
	{
		var    a = Move.Standard(E2, E4, PlayerColor.White, ChessPieceType.Pawn, MoveKind.Normal);
		object b = Move.Standard(A7, A8, PlayerColor.White, ChessPieceType.Pawn, MoveKind.Normal);
		Assert.That(a.Equals(b), Is.False);
	}

#endregion

#endregion

#region Happy paths

	[Test]
	public void NormalMove_HasExpectedProperties()
	{
		var move = Move.Standard(E2, E4, PlayerColor.White, ChessPieceType.Pawn, MoveKind.Normal);

		Assert.Multiple(
			() =>
			{
				Assert.That(move.From,        Is.EqualTo(E2));
				Assert.That(move.To,          Is.EqualTo(E4));
				Assert.That(move.Kind,        Is.EqualTo(MoveKind.Normal));
				Assert.That(move.IsPromotion, Is.False);
				Assert.That(move.PromoteTo,   Is.EqualTo(PromotionPieceType.None));
			});
	}

	[Test]
	public void PromotionFactory_CreatesValidPromotionMove()
	{
		var move = Move.PromotionQuiet(A7, A8, PlayerColor.Black, PromotionPieceType.Queen);

		Assert.Multiple(
			() =>
			{
				Assert.That(move.IsPromotion, Is.True);
				Assert.That(move.Kind,        Is.EqualTo(MoveKind.PromotionQuiet));
				Assert.That(move.PromoteTo,   Is.EqualTo(PromotionPieceType.Queen));
			});
	}

	[Test]
	public void Equality_WorksForIdenticalMoves()
	{
		var a = Move.Standard(E2, E4, PlayerColor.White, ChessPieceType.Pawn, MoveKind.Normal);
		var b = Move.Standard(E2, E4, PlayerColor.White, ChessPieceType.Pawn, MoveKind.Normal);

		Assert.Multiple(
			() =>
			{
				Assert.That(a == b,          Is.True);
				Assert.That(a.Equals(b),     Is.True);
				Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
			});
	}

	[Test]
	public void Inequality_ReturnsFalseForIdenticalMoves()
	{
		var a = Move.Standard(E2, E4, PlayerColor.White, ChessPieceType.Pawn, MoveKind.Normal);
		Assert.That(a != a, Is.False);
	}

	[Test]
	public void Deconstruct_ReturnsComponents()
	{
		var movingSide = PlayerColor.White;
		var pieceType  = ChessPieceType.Pawn;
		var move       = Move.Standard(E2, E4, movingSide, ChessPieceType.Pawn, MoveKind.Normal);
		move.Deconstruct(
			out var from, out var to, out movingSide, out pieceType, out var kind, out var promoteTo, out var check);

		Assert.Multiple(
			() =>
			{
				Assert.That(from,      Is.EqualTo(E2));
				Assert.That(to,        Is.EqualTo(E4));
				Assert.That(kind,      Is.EqualTo(MoveKind.Normal));
				Assert.That(promoteTo, Is.EqualTo(PromotionPieceType.None));
			});
	}

	[Test]
	public void ToString_NormalMove_OmitsPromotionSuffix()
	{
		var move = Move.Standard(E2, E4, PlayerColor.White, ChessPieceType.Pawn, MoveKind.Normal);
		Assert.That(move.ToString(), Is.EqualTo("e2→e4"));
	}

	[Test]
	public void ToString_PromotionMove_AppendsPromotionInfo()
	{
		var move = Move.PromotionQuiet(A7, A8, PlayerColor.Black, PromotionPieceType.Rook);
		Assert.That(move.ToString(), Does.Contain("promote to Rook"));
	}

#endregion
}
