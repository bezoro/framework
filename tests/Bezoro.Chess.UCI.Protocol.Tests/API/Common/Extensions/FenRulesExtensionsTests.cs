using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.Tests.Attributes;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Common.Extensions;

[TestSubject(typeof(FenRulesExtensions))]
public sealed class FenRulesExtensionsTests
{
	[Theory]
	[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 20)]
	[InlineData("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 48)]
	[InlineData("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 14)]
	public void GetLegalMoves_WhenPositionIsKnownPerftDepthOne_ShouldReturnExpectedCount(
		string rawFen,
		int    expectedCount)
	{
		var fen = Fen.Parse(rawFen)!.Value;

		var legalMoves = fen.GetLegalMoves();

		legalMoves.Should().HaveCount(expectedCount);
	}

	[Fact]
	public void GetLegalMoves_WhenPositionIsInitial_ShouldContainRepresentativeMoves()
	{
		var legalMoves = Fen.Default.GetLegalMoves();

		legalMoves.Should().Contain(["e2e4", "d2d4", "g1f3", "b1c3"]);
	}

	[Fact]
	public void GetLegalMoves_WhenRookIsPinnedToKing_ShouldKeepRookOnPinLine()
	{
		var fen = Fen.Parse("k3r3/8/8/8/8/8/4R3/4K3 w - - 0 1")!.Value;

		var legalMoves = fen.GetLegalMoves();

		legalMoves.Where(move => move.StartsWith("e2", StringComparison.Ordinal))
			.Should().OnlyContain(move => move[2] == 'e');
		legalMoves.Should().Contain("e2e3");
		legalMoves.Should().NotContain("e2d2");
	}

	[Theory]
	[InlineData("4k3/8/8/8/8/8/8/4K3 w K - 0 1")]
	[InlineData("k3r3/8/8/8/8/8/8/R3K2R w KQ - 0 1")]
	[InlineData("4kr2/8/8/8/8/8/8/R3K2R w KQ - 0 1")]
	public void GetLegalMoves_WhenKingsideCastlingRequirementsAreNotMet_ShouldExcludeCastling(string rawFen)
	{
		var fen = Fen.Parse(rawFen)!.Value;

		var legalMoves = fen.GetLegalMoves();

		legalMoves.Should().NotContain("e1g1");
	}

	[Fact]
	public void GetLegalMoves_WhenEnPassantWouldExposeKing_ShouldExcludeEnPassant()
	{
		var fen = Fen.Parse("k3r3/8/8/3pP3/8/8/8/4K3 w - d6 0 1")!.Value;

		var legalMoves = fen.GetLegalMoves();

		legalMoves.Should().NotContain("e5d6");
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
	public void ApplyMove_WhenMoveIsQueensideCastling_ShouldReturnUpdatedFen()
	{
		var fen = Fen.Parse("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1")!.Value;

		var next = fen.ApplyMove("e1c1");

		next.Raw.Should().Be("r3k2r/8/8/8/8/8/8/2KR3R b kq - 1 1");
	}

	[Fact]
	public void ApplyMove_WhenMoveIsCapturePromotion_ShouldPromoteAndResetClock()
	{
		var fen = Fen.Parse("1r5k/P7/8/8/8/8/8/K7 w - - 9 12")!.Value;

		var next = fen.ApplyMove("a7b8q");

		next.Raw.Should().Be("1Q5k/8/8/8/8/8/8/K7 b - - 0 12");
	}

	[Fact]
	public void ApplyMove_WhenRookCapturesCornerRook_ShouldRemoveBothCastlingRights()
	{
		var fen = Fen.Parse("r3k2r/8/8/8/8/8/8/4K2R w Kkq - 0 1")!.Value;

		var next = fen.ApplyMove("h1h8");

		next.Raw.Should().Be("r3k2R/8/8/8/8/8/8/4K3 b q - 0 1");
	}

	[Fact]
	public void ApplyMove_WhenBlackMakesQuietMove_ShouldAdvanceBothClocks()
	{
		var fen = Fen.Parse("4k1n1/8/8/8/8/8/8/4K3 b - - 7 12")!.Value;

		var next = fen.ApplyMove("g8f6");

		next.Raw.Should().Be("4k3/8/5n2/8/8/8/8/4K3 w - - 8 13");
	}

	[Fact]
	public void GetLegalMoves_WhenMoveIsPromotionCandidate_ShouldReturnAllPromotionChoices()
	{
		var fen = Fen.Parse("1r5k/P7/8/8/8/8/8/K7 w - - 0 1")!.Value;

		var legalMoves = fen.GetLegalMoves();

		legalMoves.Where(move => move.StartsWith("a7a8", StringComparison.Ordinal))
			.Should().Equal("a7a8q", "a7a8r", "a7a8b", "a7a8n");
	}

	[Theory]
	[InlineData("", typeof(ArgumentException), "Move must not be blank. (Parameter 'move')")]
	[InlineData("e2e9", typeof(ArgumentException), "Move must be valid UCI notation. (Parameter 'move')")]
	[InlineData("e2e5", typeof(InvalidOperationException), "The move is not legal in the supplied FEN position.")]
	public void ApplyMove_WhenMoveIsInvalid_ShouldThrowExactException(
		string move,
		Type   exceptionType,
		string message)
	{
		Action act = () => Fen.Default.ApplyMove(move);

		var exception = act.Should().Throw<Exception>().Which;
		exception.GetType().Should().Be(exceptionType);
		exception.Message.Should().Be(message);
	}
}
