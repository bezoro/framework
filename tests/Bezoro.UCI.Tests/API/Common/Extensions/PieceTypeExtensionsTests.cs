using Bezoro.UCI.API.Common.Enums;
using Bezoro.UCI.API.Common.Extensions;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.API.Common.Extensions;

[TestSubject(typeof(PieceTypeExtensions))]
public class PieceTypeExtensionsTests
{
	[Theory]
	[InlineData(PieceType.Pawn,   PieceColor.White, 'P')]
	[InlineData(PieceType.Knight, PieceColor.White, 'N')]
	[InlineData(PieceType.Bishop, PieceColor.White, 'B')]
	[InlineData(PieceType.Rook,   PieceColor.White, 'R')]
	[InlineData(PieceType.Queen,  PieceColor.White, 'Q')]
	[InlineData(PieceType.King,   PieceColor.White, 'K')]
	[InlineData(PieceType.Pawn,   PieceColor.Black, 'p')]
	[InlineData(PieceType.Knight, PieceColor.Black, 'n')]
	[InlineData(PieceType.Bishop, PieceColor.Black, 'b')]
	[InlineData(PieceType.Rook,   PieceColor.Black, 'r')]
	[InlineData(PieceType.Queen,  PieceColor.Black, 'q')]
	[InlineData(PieceType.King,   PieceColor.Black, 'k')]
	public void ToChar_WhenCalled_ShouldReturnCorrectCharacter_ForValidPieceTypeAndColor(
		PieceType  type,
		PieceColor color,
		char       expected)
	{
		// Act
		var result = type.ToChar(color);

		// Assert
		result.Should().Be(expected);
	}

	[Fact]
	public void ToChar_WhenCalled_ShouldThrowArgumentOutOfRangeException_ForEmptyPieceType()
	{
		// Arrange
		var type = PieceType.Empty;

		// Act
		var act = () => type.ToChar(PieceColor.White);

		// Assert
		act.Should().Throw<ArgumentOutOfRangeException>()
		   .WithMessage("Cannot convert PieceType.Empty or unknown type to a character.*");
	}
}
