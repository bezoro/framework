using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Game.Models;

[TestFixture]
public class BoardModelTests
{
#region Test Methods

	[Test]
	public void GetAllLegalMovesForSide_ReturnsCorrectNumberOfMovesForInitialPosition()
	{
		// Arrange
		var game  = new GameModel(); // Standard initial position
		var board = game.Board;

		// Act
		var whiteMoves = board.GetAllLegalMovesForSide(game, PlayerColor.White).ToList();
		var blackMoves = board.GetAllLegalMovesForSide(game, PlayerColor.Black).ToList();

		// Get total move count by flattening the collections
		var whiteMoveCount = whiteMoves.Sum(moveSet => moveSet.Count());
		var blackMoveCount = blackMoves.Sum(moveSet => moveSet.Count());

		// Assert
		// Standard chess has 20 legal moves for each side in the starting position
		// (8 pawns with 2 moves each + 2 knights with 2 moves each)
		Assert.That(whiteMoveCount, Is.EqualTo(20), "White should have 20 legal moves in the starting position");
		Assert.That(blackMoveCount, Is.EqualTo(20), "Black should have 20 legal moves in the starting position");

		// Verify correct piece types have moves
		var piecesWithMoves = whiteMoves
							  .Where(moveSet => moveSet.Any())
							  .Select(moveSet => moveSet.First().PieceType)
							  .Distinct()
							  .ToList();

		Assert.That(piecesWithMoves, Has.Count.EqualTo(2),              "Only pawns and knights should have moves");
		Assert.That(piecesWithMoves, Has.Member(ChessPieceType.Pawn),   "Pawns should have moves");
		Assert.That(piecesWithMoves, Has.Member(ChessPieceType.Knight), "Knights should have moves");
	}

#endregion
}
