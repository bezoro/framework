using Bezoro.Chess.Application.Features.PlayGame;
using Bezoro.Chess.Domain;
using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Rules;
using FluentAssertions;

namespace Bezoro.Chess.Tests.Unit;

public class GameStatusCheckerUnitTests
{
	[Fact]
	public void IsDrawByInsufficientMaterial_BishopsOnOppositeColoredSquares_ShouldReturnFalse()
	{
		// Arrange
		var initialBoard = new Piece[8, 8];

		// Set up kings
		initialBoard[0, 4] = new(PieceType.King, PieceColor.Black);
		initialBoard[7, 4] = new(PieceType.King, PieceColor.White);

		// Add bishops on opposite-colored squares
		// c1 is a dark square (row 7, col 2) -> 7+2=9 (odd)
		// f1 is a light square (row 7, col 5) -> 7+5=12 (even)
		initialBoard[7, 2] = new(PieceType.Bishop, PieceColor.White); // dark square bishop
		initialBoard[7, 5] = new(PieceType.Bishop, PieceColor.White); // light square bishop

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = PieceColor.White
		};

		var gameManager   = new GameManager(gameState);
		var statusChecker = new GameStatusChecker(gameManager);

		// Act
		bool result = statusChecker.IsDrawByInsufficientMaterial();

		// Assert
		result.Should().BeFalse("because a king and bishops on opposite-colored squares can force checkmate");
	}

	[Fact]
	public void IsDrawByInsufficientMaterial_BishopsOnSameColoredSquares_ShouldReturnTrue()
	{
		// Arrange
		var initialBoard = new Piece[8, 8];

		// Set up kings
		initialBoard[0, 4] = new(PieceType.King, PieceColor.Black);
		initialBoard[7, 4] = new(PieceType.King, PieceColor.White);

		// Add bishops on same-colored squares (dark squares: c1, e3)
		// c1 is a dark square (row 7, col 2) -> 7+2=9 (odd)
		// e3 is a dark square (row 5, col 4) -> 5+4=9 (odd)
		initialBoard[7, 2] = new(PieceType.Bishop, PieceColor.White);
		initialBoard[5, 4] = new(PieceType.Bishop, PieceColor.Black);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = PieceColor.White
		};

		var gameManager   = new GameManager(gameState);
		var statusChecker = new GameStatusChecker(gameManager);

		// Act
		bool result = statusChecker.IsDrawByInsufficientMaterial();

		// Assert
		result.Should().BeTrue("because bishops on same-colored squares cannot force checkmate");
	}

	[Fact]
	public void IsDrawByInsufficientMaterial_KingsOnly_ShouldReturnTrue()
	{
		// Arrange
		var initialBoard = new Piece[8, 8];

		// Set up kings only
		initialBoard[0, 4] = new(PieceType.King, PieceColor.Black);
		initialBoard[7, 4] = new(PieceType.King, PieceColor.White);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = PieceColor.White
		};

		var gameManager   = new GameManager(gameState);
		var statusChecker = new GameStatusChecker(gameManager);

		// Act
		bool result = statusChecker.IsDrawByInsufficientMaterial();

		// Assert
		result.Should().BeTrue("because king vs king is a theoretical draw");
	}

	[Fact]
	public void IsDrawByInsufficientMaterial_KingVsKingAndKnight_ShouldReturnTrue()
	{
		// Arrange
		var initialBoard = new Piece[8, 8];

		// Set up kings
		initialBoard[0, 4] = new(PieceType.King, PieceColor.Black);
		initialBoard[7, 4] = new(PieceType.King, PieceColor.White);

		// Add a single knight (white)
		initialBoard[7, 1] = new(PieceType.Knight, PieceColor.White);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = PieceColor.White
		};

		// Create a real GameManager with our test game state
		var gameManager = new GameManager(gameState);

		var statusChecker = new GameStatusChecker(gameManager);

		// Act
		bool result = statusChecker.IsDrawByInsufficientMaterial();

		// Assert
		result.Should().BeTrue("because a king and knight cannot checkmate a lone king");
	}

	[Fact]
	public void IsDrawByInsufficientMaterial_KingVsKingAndTwoKnights_ShouldReturnTrue()
	{
		// Arrange
		var initialBoard = new Piece[8, 8];

		// Set up kings
		initialBoard[0, 4] = new(PieceType.King, PieceColor.Black);
		initialBoard[7, 4] = new(PieceType.King, PieceColor.White);

		// Add two knights (white)
		initialBoard[7, 1] = new(PieceType.Knight, PieceColor.White);
		initialBoard[7, 6] = new(PieceType.Knight, PieceColor.White);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = PieceColor.White
		};

		var gameManager   = new GameManager(gameState);
		var statusChecker = new GameStatusChecker(gameManager);

		// Act
		bool result = statusChecker.IsDrawByInsufficientMaterial();

		// Assert
		result.Should().BeTrue("because a king and two knights cannot force checkmate against a lone king");
	}

	[Theory]
	[InlineData(PieceType.Queen)]
	[InlineData(PieceType.Rook)]
	[InlineData(PieceType.Pawn)]
	public void IsDrawByInsufficientMaterial_WithMatingPiece_ShouldReturnFalse(PieceType pieceType)
	{
		// Arrange
		var initialBoard = new Piece[8, 8];

		// Set up kings
		initialBoard[0, 4] = new(PieceType.King, PieceColor.Black);
		initialBoard[7, 4] = new(PieceType.King, PieceColor.White);

		// Add a piece that can deliver checkmate
		initialBoard[6, 3] = new(pieceType, PieceColor.White);

		var gameState = new GameState
		{
			PiecePositions = initialBoard,
			ActiveColor    = PieceColor.White
		};

		var gameManager   = new GameManager(gameState);
		var statusChecker = new GameStatusChecker(gameManager);

		// Act
		bool result = statusChecker.IsDrawByInsufficientMaterial();

		// Assert
		result.Should().BeFalse($"because a king and {pieceType} can force checkmate against a lone king");
	}
}
