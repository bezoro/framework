using Bezoro.Chess.Common.Helpers;

namespace Bezoro.Chess.UnitTests.Common.Helpers;

[TestFixture]
[TestOf(typeof(DirectionVectors))]
public class DirectionVectorsUnitTests
{
#region Test Methods

	[Test]
	public void AllSliding_IdenticalToQueenDirections()
	{
		var allSlidingDirections = DirectionVectors.AllSliding.ToArray();
		var queenDirections      = DirectionVectors.Queen.ToArray();

		Assert.That(allSlidingDirections.Length, Is.EqualTo(queenDirections.Length));

		foreach (var direction in queenDirections)
		{
			Assert.That(allSlidingDirections, Contains.Item(direction));
		}
	}

	[Test]
	public void Diagonal_ContainsFourDirections()
	{
		Assert.That(DirectionVectors.DIAGONAL.Length, Is.EqualTo(DirectionVectors.DirectionCounts.Diagonal));

		Assert.That(DirectionVectors.DIAGONAL, Contains.Item((-1, -1))); // Down-Left
		Assert.That(DirectionVectors.DIAGONAL, Contains.Item((1, -1)));  // Down-Right
		Assert.That(DirectionVectors.DIAGONAL, Contains.Item((-1, 1)));  // Up-Left
		Assert.That(DirectionVectors.DIAGONAL, Contains.Item((1, 1)));   // Up-Right
	}

	[Test]
	public void King_CombinesOrthogonalAndDiagonal()
	{
		Assert.That(DirectionVectors.KING.Length, Is.EqualTo(DirectionVectors.DirectionCounts.King));

		// Verify all orthogonal directions are included
		foreach (var direction in DirectionVectors.ORTHOGONAL)
		{
			Assert.That(DirectionVectors.KING, Contains.Item(direction));
		}

		// Verify all diagonal directions are included
		foreach (var direction in DirectionVectors.DIAGONAL)
		{
			Assert.That(DirectionVectors.KING, Contains.Item(direction));
		}
	}

	[Test]
	public void Knight_ContainsEightLShapedMoves()
	{
		Assert.That(DirectionVectors.KNIGHT.Length, Is.EqualTo(DirectionVectors.DirectionCounts.Knight));

		// Upper half
		Assert.That(DirectionVectors.KNIGHT, Contains.Item((-2, -1)));
		Assert.That(DirectionVectors.KNIGHT, Contains.Item((-1, -2)));
		Assert.That(DirectionVectors.KNIGHT, Contains.Item((1, -2)));
		Assert.That(DirectionVectors.KNIGHT, Contains.Item((2, -1)));

		// Lower half
		Assert.That(DirectionVectors.KNIGHT, Contains.Item((2, 1)));
		Assert.That(DirectionVectors.KNIGHT, Contains.Item((1, 2)));
		Assert.That(DirectionVectors.KNIGHT, Contains.Item((-1, 2)));
		Assert.That(DirectionVectors.KNIGHT, Contains.Item((-2, 1)));
	}

	[Test]
	public void Orthogonal_ContainsFourDirections()
	{
		Assert.That(DirectionVectors.ORTHOGONAL.Length, Is.EqualTo(DirectionVectors.DirectionCounts.Orthogonal));

		Assert.That(DirectionVectors.ORTHOGONAL, Contains.Item((-1, 0))); // Left
		Assert.That(DirectionVectors.ORTHOGONAL, Contains.Item((1, 0)));  // Right
		Assert.That(DirectionVectors.ORTHOGONAL, Contains.Item((0, -1))); // Down
		Assert.That(DirectionVectors.ORTHOGONAL, Contains.Item((0, 1)));  // Up
	}

	[Test]
	public void Queen_CombinesOrthogonalAndDiagonal()
	{
		var queenDirections = DirectionVectors.Queen.ToArray();
		Assert.That(queenDirections.Length, Is.EqualTo(DirectionVectors.DirectionCounts.Queen));

		// Verify all orthogonal directions are included
		foreach (var direction in DirectionVectors.ORTHOGONAL)
		{
			Assert.That(queenDirections, Contains.Item(direction));
		}

		// Verify all diagonal directions are included
		foreach (var direction in DirectionVectors.DIAGONAL)
		{
			Assert.That(queenDirections, Contains.Item(direction));
		}
	}

#endregion
}
