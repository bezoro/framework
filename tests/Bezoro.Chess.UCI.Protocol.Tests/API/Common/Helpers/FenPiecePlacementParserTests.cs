using Bezoro.Chess.UCI.Protocol.API.Common.Helpers;
using Bezoro.Chess.UCI.Protocol.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Common.Helpers;

[TestSubject(typeof(FenPiecePlacementParser))]
public sealed class FenPiecePlacementParserTests
{
	private readonly Dictionary<ChessSquare, char> _pieces = new();

	[Fact]
	public void TryParse_WhenFullFenIsProvided_ShouldPopulateOccupiedSquares()
	{
		var parsed = FenPiecePlacementParser.TryParse(
			"8/8/3k4/8/8/4K3/8/8 w - - 0 1",
			_pieces);

		parsed.Should().BeTrue();
		_pieces.Should().HaveCount(2);
		_pieces[new(3, 5)].Should().Be('k');
		_pieces[new(4, 2)].Should().Be('K');
	}

	[Fact]
	public void TryParse_WhenPlacementIsInvalid_ShouldClearOutputAndReturnFalse()
	{
		_pieces[new(0, 0)] = 'K';

		var parsed = FenPiecePlacementParser.TryParse("8/8/8/8/8/8/8", _pieces);

		parsed.Should().BeFalse();
		_pieces.Should().BeEmpty();
	}

	[Theory]
	[InlineData("11/8/8/8/8/8/8/8")]
	[InlineData("9/8/8/8/8/8/8/8")]
	[InlineData("x7/8/8/8/8/8/8/8")]
	public void TryParse_WhenRankContentIsInvalid_ShouldReturnFalse(string placement)
	{
		var parsed = FenPiecePlacementParser.TryParse(placement, _pieces);

		parsed.Should().BeFalse();
		_pieces.Should().BeEmpty();
	}

	[Fact]
	public void TryParse_WhenOutputIsNull_ShouldThrow()
	{
		var act = () => FenPiecePlacementParser.TryParse("8/8/8/8/8/8/8/8", null!);

		act.Should().Throw<ArgumentNullException>()
		   .WithParameterName("output");
	}
}
