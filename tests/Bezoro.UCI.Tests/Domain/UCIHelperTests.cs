using Bezoro.UCI.Domain.Common.Helpers;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(UciHelper))]
public static class UciHelperTests
{
	public class Unit
	{
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
			Assert.Null(result);
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
			Assert.Equal(expectedColor, result);
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
			Assert.True(result);
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
			Assert.False(result);
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
			Assert.True(result);
		}
	}
}
