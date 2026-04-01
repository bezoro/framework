using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.Tests.Attributes;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Common.Extensions;

[TestSubject(typeof(FenRulesExtensions))]
public sealed class FenRulesExtensionsTests
{
	[Fact]
	public void GetLegalMoves_WhenPositionIsInitial_ShouldReturnTwentyMoves()
	{
		var fen = Fen.Default;

		var legalMoves = fen.GetLegalMoves();

		legalMoves.Should().HaveCount(20);
		legalMoves.Should().Contain(["e2e4", "d2d4", "g1f3", "b1c3"]);
	}

	[Fact]
	public void ApplyMove_WhenMoveIsDoublePawnPush_ShouldReturnUpdatedFen()
	{
		var fen = Fen.Default;

		var next = fen.ApplyMove("e2e4");

		next.Raw.Should().Be("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1");
	}

	[Fact]
	public void ApplyMove_WhenMoveIsEnPassant_ShouldReturnUpdatedFen()
	{
		var fen = Fen.Parse("7k/8/8/3pP3/8/8/8/K7 w - d6 0 1")!.Value;

		var next = fen.ApplyMove("e5d6");

		next.Raw.Should().Be("7k/8/3P4/8/8/8/8/K7 b - - 0 1");
	}

	[Fact]
	public void ApplyMove_WhenMoveIsKingsideCastling_ShouldReturnUpdatedFen()
	{
		var fen = Fen.Parse("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1")!.Value;

		var next = fen.ApplyMove("e1g1");

		next.Raw.Should().Be("r3k2r/8/8/8/8/8/8/R4RK1 b kq - 1 1");
	}

	[Fact]
	public void GetLegalMoves_WhenMoveIsPromotionCandidate_ShouldReturnAllPromotionChoices()
	{
		var fen = Fen.Parse("1r5k/P7/8/8/8/8/8/K7 w - - 0 1")!.Value;

		var legalMoves = fen.GetLegalMoves();

		legalMoves.Should().Contain(["a7a8q", "a7a8r", "a7a8b", "a7a8n"]);
	}
}
