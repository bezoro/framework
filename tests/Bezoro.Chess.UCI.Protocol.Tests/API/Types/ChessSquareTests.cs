using Bezoro.Chess.UCI.Protocol.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Types;

[TestSubject(typeof(ChessSquare))]
public sealed class ChessSquareTests
{
	[Theory]
	[InlineData("a1", 0, 0, 'a', 1)]
	[InlineData("E4", 4, 3, 'e', 4)]
	[InlineData("h8", 7, 7, 'h', 8)]
	public void TryParse_WhenSquareIsValid_ShouldReturnSquare(
		string value,
		int    expectedFileIndex,
		int    expectedRankIndex,
		char   expectedFile,
		int    expectedRank)
	{
		var parsed = ChessSquare.TryParse(value, out var square);

		parsed.Should().BeTrue();
		square.FileIndex.Should().Be(expectedFileIndex);
		square.RankIndex.Should().Be(expectedRankIndex);
		square.File.Should().Be(expectedFile);
		square.Rank.Should().Be(expectedRank);
	}

	[Theory]
	[InlineData("")]
	[InlineData("a0")]
	[InlineData("i1")]
	[InlineData("a9")]
	[InlineData("e2e4")]
	public void TryParse_WhenSquareIsInvalid_ShouldReturnFalse(string? value)
	{
		var parsed = ChessSquare.TryParse(value, out var square);

		parsed.Should().BeFalse();
		square.Should().Be(default(ChessSquare));
	}

	[Fact]
	public void ToString_WhenSquareIsCreated_ShouldReturnAlgebraicNotation()
	{
		var square = new ChessSquare(4, 3);

		square.ToString().Should().Be("e4");
	}

	[Theory]
	[InlineData(0, 7, 'w', true)]
	[InlineData(0, 0, 'w', false)]
	[InlineData(0, 0, 'b', true)]
	[InlineData(0, 7, 'b', false)]
	public void IsPromotionRankFor_WhenColorIsSupported_ShouldReportFinalRank(
		int  fileIndex,
		int  rankIndex,
		char color,
		bool expected)
	{
		var square = new ChessSquare(fileIndex, rankIndex);

		square.IsPromotionRankFor(color).Should().Be(expected);
	}

	[Fact]
	public void IsPromotionRankFor_WhenColorIsInvalid_ShouldThrow()
	{
		var square = new ChessSquare(0, 0);

		Action act = () => square.IsPromotionRankFor('x');

		act.Should().Throw<ArgumentException>()
		   .WithMessage("*Expected 'w' or 'b'*");
	}
}
