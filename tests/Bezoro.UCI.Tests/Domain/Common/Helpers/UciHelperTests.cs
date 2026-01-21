using Bezoro.UCI.Domain.Common.Constants;
using Bezoro.UCI.Domain.Common.Helpers;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit.Abstractions;

namespace Bezoro.UCI.Tests.Domain.Common.Helpers;

[TestSubject(typeof(UciHelper))]
public class UciHelperTests(ITestOutputHelper output) : UnitTestBase(output)
{
	[Fact]
	public void GetPlayerColorFromFen_WhenBlackToMove_ShouldReturnB()
	{
		Log("Testing GetPlayerColorFromFen with black to move");
		string fen = UciConstants.Fen.BLACK_MATE_IN_ONE;

		char? result = UciHelper.GetPlayerColorFromFen(fen);

		result.Should().Be('b');
	}

	[Theory]
	[InlineData("")]
	[InlineData("invalid")]
	[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR")]
	[InlineData("   ")]
	public void GetPlayerColorFromFen_WhenInvalidFen_ShouldReturnNull(string invalidFen)
	{
		Log("Testing GetPlayerColorFromFen with invalid FEN: {0}", invalidFen);
		char? result = UciHelper.GetPlayerColorFromFen(invalidFen);

		result.Should().BeNull();
	}

	[Theory]
	[InlineData(" ")]
	[InlineData("rnbqkbnr")]
	[InlineData("8/8/8/8/8/8/8/8 x - - 0 1")]
	public void GetPlayerColorFromFen_WhenInvalidFormat_ShouldReturnNull(string fen)
	{
		Log("Testing GetPlayerColorFromFen with invalid format: {0}", fen);
		char? result = UciHelper.GetPlayerColorFromFen(fen);

		result.Should().BeNull();
	}

	[Theory]
	[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",    'w')]
	[InlineData("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1", 'b')]
	[InlineData("8/2k5/8/8/8/8/3K4/8 w - - 0 1",                               'w')]
	[InlineData("8/2k5/8/8/8/8/3K4/8 b - - 0 1",                               'b')]
	public void GetPlayerColorFromFen_WhenValidFen_ShouldReturnCorrectColor(string fen, char expectedColor)
	{
		Log("Testing GetPlayerColorFromFen with valid FEN");
		char? result = UciHelper.GetPlayerColorFromFen(fen);

		result.Should().Be(expectedColor);
	}

	[Fact]
	public void GetPlayerColorFromFen_WhenWhiteToMove_ShouldReturnW()
	{
		Log("Testing GetPlayerColorFromFen with white to move");
		string fen = UciConstants.Fen.WHITE_MATE_IN_ONE;

		char? result = UciHelper.GetPlayerColorFromFen(fen);

		result.Should().Be('w');
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("i1")]
	[InlineData("a9")]
	[InlineData("1a")]
	[InlineData("e0")]
	[InlineData("z9")]
	public void IsValidAlgebraicNotation_WhenInvalidSquare_ShouldReturnFalse(string square)
	{
		Log("Testing IsValidAlgebraicNotation with invalid square: {0}", square);
		bool result = UciHelper.IsValidAlgebraicNotation(square);

		result.Should().BeFalse();
	}

	[Theory]
	[InlineData("a1")]
	[InlineData("h8")]
	[InlineData("e4")]
	[InlineData("b2")]
	[InlineData("g5")]
	public void IsValidAlgebraicNotation_WhenValidSquare_ShouldReturnTrue(string square)
	{
		Log("Testing IsValidAlgebraicNotation with valid square: {0}", square);
		bool result = UciHelper.IsValidAlgebraicNotation(square);

		result.Should().BeTrue();
	}

	[Theory]
	[InlineData("   ")]
	[InlineData("invalid")]
	[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR")]
	[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w")]
	[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR x KQkq - 0 1")]
	public void IsValidFen_WhenInvalidFen_ShouldReturnFalse(string invalidFen)
	{
		Log("Testing IsValidFen with invalid FEN: {0}", invalidFen);
		bool result = UciHelper.IsValidFen(invalidFen);

		result.Should().BeFalse();
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("invalid fen")]
	[InlineData("rnbqkbnr")]
	public void IsValidFen_WhenInvalidInput_ShouldReturnFalse(string fen)
	{
		Log("Testing IsValidFen with invalid input: {0}", fen);
		bool result = UciHelper.IsValidFen(fen);

		result.Should().BeFalse();
	}

	[Fact]
	public void IsValidFen_WhenStandardFen_ShouldReturnTrue()
	{
		Log("Testing IsValidFen with standard FEN");
		string fen = UciConstants.Fen.STANDARD;

		bool result = UciHelper.IsValidFen(fen);

		result.Should().BeTrue();
	}

	[Theory]
	[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1")]
	[InlineData("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1")]
	[InlineData("8/2k5/8/8/8/8/3K4/8 w - - 0 1")]
	public void IsValidFen_WhenValidFen_ShouldReturnTrue(string validFen)
	{
		Log("Testing IsValidFen with valid FEN");
		bool result = UciHelper.IsValidFen(validFen);

		result.Should().BeTrue();
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("abcd")]
	[InlineData("e9e4")]
	[InlineData("a1a1qq")]
	public void IsValidUciMove_WhenInvalidMove_ShouldReturnFalse(string move)
	{
		Log("Testing IsValidUciMove with invalid move: {0}", move);
		bool result = UciHelper.IsValidUciMove(move);

		result.Should().BeFalse();
	}

	[Fact]
	public void IsValidUciMove_WhenNull_ShouldReturnFalse()
	{
		Log("Testing IsValidUciMove with null");
		bool result = UciHelper.IsValidUciMove(null);

		result.Should().BeFalse();
	}

	[Theory]
	[InlineData("e2e4")]
	[InlineData("b7b8q")]
	public void IsValidUciMove_WhenValidMove_ShouldReturnTrue(string move)
	{
		Log("Testing IsValidUciMove with valid move: {0}", move);
		bool result = UciHelper.IsValidUciMove(move);

		result.Should().BeTrue();
	}
}
