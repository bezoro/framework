using System.Numerics;
using Bezoro.Chess.Board;

namespace Bezoro.Chess.UnitTests;

[TestFixture]
public class BoardPositionTests
{
#region Happy paths

	[Test]
	public void Ctor_FromCoordinates_ComputesAlgebraic()
	{
		var pos = new BoardPosition(4, 3); // e4
		Assert.That(pos.Algebraic, Is.EqualTo("e4"));
	}

	[Test]
	public void Ctor_FromAlgebraic_ParsesCorrectly()
	{
		var pos = new BoardPosition("h1");
		Assert.Multiple(
			() =>
			{
				Assert.That(pos.Column,    Is.EqualTo(7));
				Assert.That(pos.Rank,      Is.EqualTo(0));
				Assert.That(pos.IsValid(), Is.True);
			});
	}

	[Test]
	public void FromVector_CreatesEquivalentPosition()
	{
		var vec = new Vector2(5, 6); // f7
		var pos = BoardPosition.FromVector(vec);
		Assert.That(pos, Is.EqualTo(new BoardPosition(5, 6)));
	}

	[Test]
	public void InequalityOperator_ComplementsEquality()
	{
		var a = new BoardPosition(0, 0);
		var b = new BoardPosition(1, 0);
		Assert.That(a != b, Is.True);
		Assert.That(a != a, Is.False); // reflexive
	}

	[Test]
	public void Equals_ObjectOverload_BehavesLikeTypedEquals()
	{
		var    a         = new BoardPosition(3, 3);
		object same      = new BoardPosition(3, 3);
		object different = new BoardPosition(2, 2);

		Assert.Multiple(
			() =>
			{
				Assert.That(a.Equals(same),         Is.True);
				Assert.That(a.Equals(different),    Is.False);
				Assert.That(a.Equals(new object()), Is.False); // unrelated type
			});
	}

	[Test]
	public void Deconstruct_ReturnsColumnAndRank()
	{
		var pos = new BoardPosition(4, 7); // e8
		pos.Deconstruct(out var file, out var rank);

		Assert.Multiple(
			() =>
			{
				Assert.That(file, Is.EqualTo(4));
				Assert.That(rank, Is.EqualTo(7));
			});
	}

	[Test]
	public void File_Property_ReturnsExpectedLetter()
	{
		var pos = new BoardPosition(0, 0);
		Assert.That(pos.File, Is.EqualTo('a'));
	}

	[Test]
	public void Vector_Property_ReturnsColumnRank()
	{
		var pos = new BoardPosition(3, 5);
		Assert.That(pos.Vector, Is.EqualTo(new Vector2(3, 5)));
	}

	[Test]
	public void Equality_WorksForIdenticalPositions()
	{
		var a = new BoardPosition(2, 2);
		var b = new BoardPosition(2, 2);

		Assert.Multiple(
			() =>
			{
				Assert.That(a == b,          Is.True);
				Assert.That(a.Equals(b),     Is.True);
				Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
			});
	}

	[Test]
	public void Equality_ConsidersBoardDimensions()
	{
		var eightByEight = new BoardPosition(1, 1); // 8×8
		var nineByNine   = new BoardPosition(1, 1, 9, 9);

		Assert.That(eightByEight.Equals(nineByNine), Is.False);
	}

	[Test]
	public void ToString_EqualsAlgebraic_ForValidSquare()
	{
		var pos = new BoardPosition(6, 2); // g3
		Assert.That(pos.ToString(), Is.EqualTo(pos.Algebraic));
	}

	[Test]
	public void NonDefaultBoardSize_AllowsLargerCoordinates()
	{
		var pos = new BoardPosition(7, 7, 9, 9); // h8 on 9×9
		Assert.Multiple(
			() =>
			{
				Assert.That(pos.IsValid(), Is.True);
				Assert.That(pos.Algebraic, Is.EqualTo("h8"));
			});
	}

#endregion

#region Sad paths

	[Test]
	public void NegativeColumn_IsInvalid()
	{
		var pos = new BoardPosition(-1, 0);
		Assert.Multiple(
			() =>
			{
				Assert.That(pos.IsValid(),  Is.False);
				Assert.That(pos.ToString(), Is.EqualTo("Invalid"));
			});
	}

	[Test]
	public void ColumnEqualToBoardWidth_IsInvalid()
	{
		var pos = new BoardPosition(8, 0); // 8×8 board, col 8 is out of bounds
		Assert.That(pos.IsValid(), Is.False);
	}

	[Test]
	public void ZeroMaxColumn_ThrowsException() =>
		Assert.Throws<ArgumentOutOfRangeException>(
			() =>
			{
				_ = new BoardPosition(0, 0, 0);
			});

#endregion
}
