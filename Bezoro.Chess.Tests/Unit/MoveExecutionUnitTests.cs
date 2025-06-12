using ChessLogic;
using FluentAssertions;

namespace Bezoro.Chess.Tests.Unit;

public class MoveExecutionUnitTests
{
	[Fact]
	public void ExecuteMove_WhitePawnE2ToE4_ShouldUpdateStateCorrectly()
	{
		// Arrange
		var gameManager  = new GameManager(); // Creates a game with the standard starting position
		var initialState = gameManager.CurrentState;
		var move         = new Move(new("e2"), new("e4"));

		// Act
		var newState = MoveExecution.ExecuteMove(initialState, move);

		// Assert
		// 1. Verify the piece moved
		var pawnAtE4 = newState.PiecePositions[new Position("e4").Row, new Position("e4").Col];
		pawnAtE4.Should().Be(new Piece(PieceType.Pawn, PieceColor.White));

		// 2. Verify the original square is empty
		var pieceAtE2 = newState.PiecePositions[new Position("e2").Row, new Position("e2").Col];
		pieceAtE2.Type.Should().Be(default); // Or PieceType.None if you have it

		// 3. Verify the active color switched to Black
		newState.ActiveColor.Should().Be(PieceColor.Black);

		// 4. Verify en passant target square is set
		newState.EnPassantTargetSquare.Should().Be(new Position("e3"));

		// 5. Verify half-move clock was reset
		newState.HalfMoveClock.Should().Be(0);

		// 6. Verify full-move number is unchanged (only increments after Black's move)
		newState.FullMoveNumber.Should().Be(1);
	}
}
