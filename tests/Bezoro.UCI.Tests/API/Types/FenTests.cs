using Bezoro.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.API.Types;

[TestSubject(typeof(Fen))]
public class FenTests
{
	[Fact]
	public void TryParseUciOutputLine_WhenInvalidFenStructure_ReturnsFalseAndNull()
	{
		const string line    = "invalid fen string";
		string?      cache   = null;
		bool         success = Fen.TryParseUciOutputLine(line, ref cache, out var fen);

		success.Should().BeFalse();
		fen.Should().BeNull();
	}

	[Fact]
	public void TryParseUciOutputLine_WhenValidFenStructure_ReturnsTrueAndValidObject()
	{
		const string line    = "fen: rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
		string?      cache   = null;
		bool         success = Fen.TryParseUciOutputLine(line, ref cache, out var fen);

		success.Should().BeTrue();
		fen?.Raw.Should().Be("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
	}

	[Fact]
	public void TryParseUciOutputLine_WhenCheckersLineAndCacheProvided_ReturnsEnrichedFen()
	{
		const string fenLine      = "fen: rnbqkbnr/pppp1ppp/8/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2";
		const string checkersLine = "checkers: e4";

		string? cache = null;
		Fen.TryParseUciOutputLine(fenLine, ref cache, out var baseFen).Should().BeTrue();

		bool success = Fen.TryParseUciOutputLine(checkersLine, ref cache, out var enrichedFen);

		success.Should().BeTrue();
		enrichedFen.Should().NotBeNull();
		enrichedFen!.Value.Checkers.Should().Be("e4");
	}
}
