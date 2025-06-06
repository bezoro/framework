using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Game.Models;

[TestFixture]
public class BoardModelTests
{
#region Test Methods

	[Test]
	public void GetAllLegalMovesForSide_FiltersAndAggregatesMovesCorrectly()
	{
		//Arrange
		var game  = new GameModel();
		var board = game.Board;

		//Act
		var moves = board.GetAllLegalMovesForSide(game, PlayerColor.White).ToList();

		//Assert
		Assert.That(moves, Is.Not.Empty);
		TestContext.WriteLine($"Successfully generated {moves.Count} legal moves for White");
		foreach (var move in moves)
		{
			foreach (var m in move)
			{
				TestContext.Out.WriteLine($"{m.MovingSide} {m.PieceType} + {m}");
			}
		}
	}

#endregion
}
