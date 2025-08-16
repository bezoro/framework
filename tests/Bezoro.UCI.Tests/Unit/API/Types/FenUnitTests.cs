using Bezoro.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Unit.API.Types;

[TestSubject(typeof(Fen))]
public class FenUnitTests
{
	[Fact]
	public void TryParseUciOutputLine_WhenInvalidLine_ReturnsFalseAndNull()
	{
		const string line    = "invalid fen string";
		bool         success = Fen.TryParseUciOutputLine(line, out var fen);

		success.Should().BeFalse();
		fen.Should().BeNull();
	}

	[Fact]
	public void TryParseUciOutputLine_WhenValidLine_ReturnsTrueAndValidObject()
	{
		const string line    = "fen rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
		bool         success = Fen.TryParseUciOutputLine(line, out var fen);

		success.Should().BeTrue();
		fen.Should().Be("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
	}
}
