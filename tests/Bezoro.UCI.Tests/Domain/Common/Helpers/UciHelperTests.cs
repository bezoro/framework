using Bezoro.UCI.Domain.Common.Constants;
using Bezoro.UCI.Domain.Common.Helpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Domain.Common.Helpers;

[TestSubject(typeof(UciHelper))]
public static class UciHelperTests
{
	public class Unit
	{
		[Fact]
		public void GetPlayerColorFromFen_ShouldReturn_b_ForBlackToMove()
		{
			// Arrange
			string fen = UciConstants.Fen.BLACK_MATE_IN_ONE; // contains 'b' active color

			// Act
			char? result = UciHelper.GetPlayerColorFromFen(fen);

			// Assert
			result.Should().Be('b');
		}

		[Fact]
		public void GetPlayerColorFromFen_ShouldReturn_w_ForWhiteToMove()
		{
			// Arrange
			string fen = UciConstants.Fen.WHITE_MATE_IN_ONE; // contains 'w' active color

			// Act
			char? result = UciHelper.GetPlayerColorFromFen(fen);

			// Assert
			result.Should().Be('w');
		}

		[Theory]
		[InlineData(" ")]                         // whitespace -> early null
		[InlineData("rnbqkbnr")]                  // not enough fields
		[InlineData("8/8/8/8/8/8/8/8 x - - 0 1")] // invalid active color field
		public void GetPlayerColorFromFen_ShouldReturnNull_ForInvalidInputs(string fen)
		{
			// Act
			char? result = UciHelper.GetPlayerColorFromFen(fen);

			// Assert
			result.Should().BeNull();
		}

		[Theory]
		[InlineData("")]
		[InlineData("invalid")]
		[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR")]
		[InlineData("   ")]
		public void GetPlayerColorFromFen_WhenInvalidFen_ReturnsNull(string invalidFen)
		{
			// Act
			char? result = UciHelper.GetPlayerColorFromFen(invalidFen);

			// Assert
			result.Should().BeNull();
		}

		[Theory]
		[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",    'w')]
		[InlineData("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1", 'b')]
		[InlineData("8/2k5/8/8/8/8/3K4/8 w - - 0 1",                               'w')]
		[InlineData("8/2k5/8/8/8/8/3K4/8 b - - 0 1",                               'b')]
		public void GetPlayerColorFromFen_WhenValidFen_ReturnsCorrectColor(string fen, char expectedColor)
		{
			// Act
			char? result = UciHelper.GetPlayerColorFromFen(fen);

			// Assert
			result.Should().Be(expectedColor);
		}

		[Theory]
		[InlineData("")]
		[InlineData(" ")]
		[InlineData("i1")]
		[InlineData("a9")]
		[InlineData("1a")]
		[InlineData("e0")]
		[InlineData("z9")]
		public void IsValidAlgebraicNotation_ShouldReturnFalse_ForInvalidSquares(string square)
		{
			// Act
			bool result = UciHelper.IsValidAlgebraicNotation(square);

			// Assert
			result.Should().BeFalse();
		}

		[Theory]
		[InlineData("a1")]
		[InlineData("h8")]
		[InlineData("e4")]
		[InlineData("b2")]
		public void IsValidAlgebraicNotation_ShouldReturnTrue_ForValidSquares(string square)
		{
			// Act
			bool result = UciHelper.IsValidAlgebraicNotation(square);

			// Assert
			result.Should().BeTrue();
		}

		[Theory]
		[InlineData("a1")]
		[InlineData("h8")]
		[InlineData("e4")]
		[InlineData("g5")]
		public void IsValidAlgebraicNotation_WhenValidNotation_ReturnsTrue(string validSquare)
		{
			// Act
			bool result = UciHelper.IsValidAlgebraicNotation(validSquare);

			// Assert
			result.Should().BeTrue();
		}

		[Theory]
		[InlineData("")]
		[InlineData(" ")]
		[InlineData("invalid fen")]
		[InlineData("rnbqkbnr")] // not enough fields
		public void IsValidFen_ShouldReturnFalse_ForInvalidInput(string fen)
		{
			// Act
			bool result = UciHelper.IsValidFen(fen);

			// Assert
			result.Should().BeFalse();
		}

		[Fact]
		public void IsValidFen_ShouldReturnTrue_ForStandardFen()
		{
			// Arrange
			string fen = UciConstants.Fen.STANDARD;

			// Act
			bool result = UciHelper.IsValidFen(fen);

			// Assert
			result.Should().BeTrue();
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData("invalid")]
		[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR")]
		[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w")]
		[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR x KQkq - 0 1")]
		public void IsValidFen_WhenInValidFen_ReturnsFalse(string invalidFen)
		{
			// Act
			bool result = UciHelper.IsValidFen(invalidFen);

			// Assert
			result.Should().BeFalse();
		}

		[Theory]
		[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1")]
		[InlineData("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1")]
		[InlineData("8/2k5/8/8/8/8/3K4/8 w - - 0 1")]
		public void IsValidFen_WhenValidFen_ReturnsTrue(string validFen)
		{
			// Act
			bool result = UciHelper.IsValidFen(validFen);

			// Assert
			result.Should().BeTrue();
		}

		[Theory]
		[InlineData("")]
		[InlineData(" ")]
		[InlineData("abcd")]   // not a valid square->square pattern
		[InlineData("e9e4")]   // rank out of bounds
		[InlineData("a1a1qq")] // too long
		public void IsValidUciMove_ShouldReturnFalse_ForInvalidMoves(string move)
		{
			// Act
			bool result = UciHelper.IsValidUciMove(move);

			// Assert
			result.Should().BeFalse();
		}

		[Fact]
		public void IsValidUciMove_ShouldReturnFalse_ForNull()
		{
			// Act
			bool result = UciHelper.IsValidUciMove(null);

			// Assert
			result.Should().BeFalse();
		}

		[Theory]
		[InlineData("e2e4")]
		[InlineData("b7b8q")] // promotion
		public void IsValidUciMove_ShouldReturnTrue_ForValidMoves(string move)
		{
			// Act
			bool result = UciHelper.IsValidUciMove(move);

			// Assert
			result.Should().BeTrue();
		}
	}
}
