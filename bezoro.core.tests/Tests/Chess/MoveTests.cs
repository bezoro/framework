using System;
using Bezoro.Core.Chess;
using Bezoro.Core.Chess.Pieces;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Chess
{
	[TestFixture]
	public class MoveTests
	{
		private static readonly BoardPosition A7 = new(0, 6); // a7
		private static readonly BoardPosition A8 = new(0, 7); // a8
		private static readonly BoardPosition E2 = new(4, 1); // e2
		private static readonly BoardPosition E4 = new(4, 3); // e4

	#region Happy paths

		[Test]
		public void NormalMove_HasExpectedProperties()
		{
			var move = new Move(E2, E4, PlayerColor.White);

			Assert.Multiple(
				() =>
				{
					Assert.That(move.From,        Is.EqualTo(E2));
					Assert.That(move.To,          Is.EqualTo(E4));
					Assert.That(move.Kind,        Is.EqualTo(MoveKind.Normal));
					Assert.That(move.IsPromotion, Is.False);
					Assert.That(move.PromoteTo,   Is.Null);
				});
		}

		[Test]
		public void PromotionFactory_CreatesValidPromotionMove()
		{
			var move = Move.Promotion(A7, A8, PlayerColor.Black, PromotionPieceType.Queen);

			Assert.Multiple(
				() =>
				{
					Assert.That(move.IsPromotion, Is.True);
					Assert.That(move.Kind,        Is.EqualTo(MoveKind.Promotion));
					Assert.That(move.PromoteTo,   Is.EqualTo(PromotionPieceType.Queen));
				});
		}

		[Test]
		public void Equality_WorksForIdenticalMoves()
		{
			var a = new Move(E2, E4, PlayerColor.White);
			var b = new Move(E2, E4, PlayerColor.White);

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
			var a = new Move(E2, E4, PlayerColor.White);
			Assert.That(a != a, Is.False); // reflexive
		}

		[Test]
		public void Deconstruct_ReturnsComponents()
		{
			var move = new Move(E2, E4, PlayerColor.White);
			move.Deconstruct(out var from, out var to, out var kind, out var promoteTo);

			Assert.Multiple(
				() =>
				{
					Assert.That(from,      Is.EqualTo(E2));
					Assert.That(to,        Is.EqualTo(E4));
					Assert.That(kind,      Is.EqualTo(MoveKind.Normal));
					Assert.That(promoteTo, Is.Null);
				});
		}

		[Test]
		public void ToString_NormalMove_OmitsPromotionSuffix()
		{
			var move = new Move(E2, E4, PlayerColor.White);
			Assert.That(move.ToString(), Is.EqualTo("e2→e4"));
		}

		[Test]
		public void ToString_PromotionMove_AppendsPromotionInfo()
		{
			var move = Move.Promotion(A7, A8, PlayerColor.Black, PromotionPieceType.Rook);
			Assert.That(move.ToString(), Does.Contain("promote to Rook"));
		}

	#endregion

	#region Sad paths

		[Test]
		public void PromotionWithoutPromoteTo_ThrowsArgumentNull() =>
			Assert.Throws<ArgumentNullException>(
				() =>
				{
					_ = new Move(A7, A8, PlayerColor.White, MoveKind.Promotion);
				});

		[Test]
		public void NonPromotionWithPromoteTo_ThrowsArgumentException() =>
			Assert.Throws<ArgumentException>(
				() =>
				{
					_ = new Move(E2, E4, PlayerColor.White, MoveKind.Normal, PromotionPieceType.Bishop);
				});

		[Test]
		public void EqualsObject_ReturnsFalseForDifferentMove()
		{
			var    a = new Move(E2, E4, PlayerColor.White);
			object b = new Move(A7, A8, PlayerColor.White);
			Assert.That(a.Equals(b), Is.False);
		}

	#endregion
	}
}
