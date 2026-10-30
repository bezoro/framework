using Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit.Abstractions;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Types;

[TestSubject(typeof(Fen))]
public class FenTests(ITestOutputHelper output) : UnitTestBase(output)
{
	[Fact]
	public void Parse_WhenFenIsInvalid_ShouldReturnNull()
	{
		Log("Testing Parse with invalid FEN");
		Fen.Parse("invalid fen string").Should().BeNull();
	}

	[Fact]
	public void Parse_WhenFenIsValid_ShouldReturnFenStruct()
	{
		Log("Testing Parse with valid FEN");
		var parsed = Fen.Parse(Fen.Default);

		parsed.Should().NotBeNull();
		parsed!.Value.Raw.Should().Be(Fen.Default);
	}

	[Fact]
	public void TryParseUciOutputLine_WhenCheckersLineAndCacheProvided_ShouldReturnEnrichedFen()
	{
		Log("Testing TryParseUciOutputLine with checkers line");
		const string FEN_LINE      = "fen: rnbqkbnr/pppp1ppp/8/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2";
		const string CHECKERS_LINE = "checkers: e4";

		string? cache = null;
		Fen.TryParseUciOutputLine(FEN_LINE, ref cache, out _).Should().BeTrue();

		bool success = Fen.TryParseUciOutputLine(CHECKERS_LINE, ref cache, out var enrichedFen);

		success.Should().BeTrue();
		enrichedFen.Should().NotBeNull();
		enrichedFen!.Value.Checkers.Should().Be("e4");
	}

	[Fact]
	public void TryParseUciOutputLine_WhenInvalidFenStructure_ShouldReturnFalseAndNull()
	{
		Log("Testing TryParseUciOutputLine with invalid FEN structure");
		const string LINE    = "invalid fen string";
		string?      cache   = null;
		bool         success = Fen.TryParseUciOutputLine(LINE, ref cache, out var fen);

		success.Should().BeFalse();
		fen.Should().BeNull();
	}

	[Fact]
	public void TryParseUciOutputLine_WhenValidFenStructure_ShouldReturnTrueAndValidObject()
	{
		Log("Testing TryParseUciOutputLine with valid FEN structure");
		const string LINE    = "fen: rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
		string?      cache   = null;
		bool         success = Fen.TryParseUciOutputLine(LINE, ref cache, out var fen);

		success.Should().BeTrue();
		fen?.Raw.Should().Be("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
	}

	[Fact]
	public void Validate_WhenFenIsInvalid_ShouldReturnFalse()
	{
		Log("Testing Validate with invalid FEN");
		Fen.Validate("not a fen at all").Should().BeFalse();
	}

	[Fact]
	public void Validate_WhenFenIsValid_ShouldReturnTrue()
	{
		Log("Testing Validate with valid FEN");
		Fen.Validate(Fen.Default).Should().BeTrue();
	}
	[Theory]
	[InlineData("8/8/8/8/8/8/8/7 w - - 0 1")]
	[InlineData("8/8/8/8/8/8/8/8K w - - 0 1")]
	[InlineData("8/8/8/8/8/8/8/44 w - - 0 1")]
	[InlineData("8/8/8/8/8/8/8/8 w - - 0 0")]
	[InlineData("8/8/8/8/8/8/8/8 w - - 2147483648 1")]
	[InlineData("8/8/8/8/8/8/8/8 w - - 0 2147483648")]
	[InlineData("8/8/8/8/8/8/8/8 w - - +0 1")]
	[InlineData("8/8/8/8/8/8/8/8 w - - 0 1 extra")]
	[InlineData("8/8/8/8/8/8/8/8\nw - - 0 1")]
	[InlineData("8/8/8/8/8/8/8/8 w KK - 0 1")]
	[InlineData("8/8/8/8/8/8/8/8 w - a4 0 1")]
	public void Parse_WhenFieldsAreMalformed_ShouldReturnNullWithoutThrowing(string rawFen)
	{
		Fen.Validate(rawFen).Should().BeFalse();
		Fen.Parse(rawFen).Should().BeNull();
	}

	[Fact]
	public void Parse_WhenFieldsUseHorizontalWhitespace_ShouldParseEveryField()
	{
		var fen = Fen.Parse("  8/8/8/8/8/8/8/8\tw  -\t-  0  1  ");

		fen.Should().NotBeNull();
		fen!.Value.ActiveColor.Should().Be('w');
		fen.Value.FullmoveNumber.Should().Be(1);
	}

	[Fact]
	public void Parse_WhenCountersReachMaximum_ShouldPreserveValues()
	{
		var fen = Fen.Parse("8/8/8/8/8/8/8/8 w - - 2147483647 2147483647");

		fen.Should().NotBeNull();
		fen!.Value.HalfmoveClock.Should().Be(int.MaxValue);
		fen.Value.FullmoveNumber.Should().Be(int.MaxValue);
	}

}
