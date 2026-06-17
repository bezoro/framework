using Bezoro.Chess.UCI.Protocol.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Types;

[TestSubject(typeof(UciCoordinateMove))]
public sealed class UciCoordinateMoveTests
{
	[Theory]
	[InlineData("e2e4", "e2", "e4", null, "e2e4")]
	[InlineData(" A7A8Q ", "a7", "a8", 'q', "a7a8q")]
	[InlineData("h2h1n", "h2", "h1", 'n', "h2h1n")]
	public void TryParse_WhenMoveIsValid_ShouldReturnNormalizedMove(
		string value,
		string expectedFrom,
		string expectedTo,
		char?  expectedPromotionPiece,
		string expectedNotation)
	{
		var parsed = UciCoordinateMove.TryParse(value, out var move);

		parsed.Should().BeTrue();
		move.From.ToString().Should().Be(expectedFrom);
		move.To.ToString().Should().Be(expectedTo);
		move.PromotionPiece.Should().Be(expectedPromotionPiece);
		move.Notation.Should().Be(expectedNotation);
		move.ToString().Should().Be(expectedNotation);
	}

	[Theory]
	[InlineData("")]
	[InlineData("e2e")]
	[InlineData("e2e45q")]
	[InlineData("e2e2")]
	[InlineData("e2e4q")]
	[InlineData("a7a6q")]
	[InlineData("a7c8q")]
	[InlineData("a7a8p")]
	public void TryParse_WhenMoveIsInvalid_ShouldReturnFalse(string? value)
	{
		var parsed = UciCoordinateMove.TryParse(value, out var move);

		parsed.Should().BeFalse();
		move.Should().Be(default(UciCoordinateMove));
	}

	[Fact]
	public void Create_WhenPromotionPieceIsProvided_ShouldReturnPromotingMove()
	{
		var move = UciCoordinateMove.Create(new(0, 6), new(0, 7), 'Q');

		move.Notation.Should().Be("a7a8q");
		move.IsPromotion.Should().BeTrue();
	}

	[Fact]
	public void Create_WhenPromotionPieceIsInvalid_ShouldThrow()
	{
		var act = () => UciCoordinateMove.Create(new(0, 6), new(0, 7), 'p');

		act.Should().Throw<ArgumentException>()
		   .WithMessage("*Promotion piece must be one of*");
	}

	[Fact]
	public void Create_WhenSourceAndTargetAreSame_ShouldThrow()
	{
		var square = new ChessSquare(4, 1);

		var act = () => UciCoordinateMove.Create(square, square);

		act.Should().Throw<ArgumentException>()
		   .WithMessage("*Source and target squares must be different*");
	}
}
