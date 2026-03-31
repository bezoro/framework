using System.Collections.Immutable;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Common.Extensions;

[TestSubject(typeof(FenMoveClassificationExtensions))]
public class FenMoveClassificationExtensionsTests
{
	[Fact]
	public void ClassifyMove_WhenPawnAdvancesTwoSquares_ShouldMarkNormalDoublePawnPush()
	{
		var classification = Fen.Default.ClassifyMove("e2e4");

		classification.MovingPiece.Should().Be('P');
		classification.IsNormal.Should().BeTrue();
		classification.IsDoublePawnPush.Should().BeTrue();
		classification.IsResolved.Should().BeFalse();
	}

	[Fact]
	public void ClassifyMove_WhenMoveIsEnPassant_ShouldMarkCaptureAndEnPassant()
	{
		var fen = Fen.Parse("8/8/8/3pP3/8/8/8/8 w - d6 0 1")!.Value;

		var classification = fen.ClassifyMove("e5d6");

		classification.IsCapture.Should().BeTrue();
		classification.IsEnPassant.Should().BeTrue();
		classification.CapturedPiece.Should().Be('p');
	}

	[Fact]
	public void ClassifyMove_WhenMoveIsKingsideCastling_ShouldMarkKingsideCastling()
	{
		var fen = Fen.Parse("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1")!.Value;

		var classification = fen.ClassifyMove("e1g1");

		classification.IsCastling.Should().BeTrue();
		classification.IsKingsideCastling.Should().BeTrue();
		classification.IsQueensideCastling.Should().BeFalse();
	}

	[Fact]
	public void ClassifyMove_WhenMoveIsPromotionCapture_ShouldMarkPromotionAndCapture()
	{
		var fen = Fen.Parse("1r6/P7/8/8/8/8/8/7k w - - 0 1")!.Value;

		var classification = fen.ClassifyMove("a7b8q");

		classification.IsPromotion.Should().BeTrue();
		classification.IsCapture.Should().BeTrue();
		classification.PromotionPiece.Should().Be('q');
		classification.CapturedPiece.Should().Be('r');
	}

	[Fact]
	public void ClassifyMoves_WhenMultipleMovesAreProvided_ShouldReturnMapForEachMove()
	{
		var classifications = Fen.Default.ClassifyMoves(ImmutableArray.Create("e2e4", "g1f3"));

		classifications.Should().ContainKey("e2e4");
		classifications.Should().ContainKey("g1f3");
	}

	[Fact]
	public void ClassifyMoveFully_WhenMoveGivesCheck_ShouldMarkCheckAndResolved()
	{
		var fen = Fen.Parse("7k/8/6Q1/8/8/8/8/7K w - - 0 1")!.Value;

		var classification = fen.ClassifyMoveFully("g6g7");

		classification.IsCheck.Should().BeTrue();
		classification.IsMate.Should().BeFalse();
		classification.IsStalemate.Should().BeFalse();
		classification.IsResolved.Should().BeTrue();
	}

	[Fact]
	public void ClassifyMoveFully_WhenMoveGivesMate_ShouldMarkMateAndResolved()
	{
		var fen = Fen.Parse("7k/5Q2/7K/8/8/8/8/8 w - - 0 1")!.Value;

		var classification = fen.ClassifyMoveFully("f7g7");

		classification.IsCheck.Should().BeTrue();
		classification.IsMate.Should().BeTrue();
		classification.IsStalemate.Should().BeFalse();
		classification.IsResolved.Should().BeTrue();
	}

	[Fact]
	public void ClassifyMoveFully_WhenMoveStalematesOpponent_ShouldMarkStalemateAndResolved()
	{
		var fen = Fen.Parse("k7/1QK5/8/8/8/8/8/8 w - - 0 1")!.Value;

		var classification = fen.ClassifyMoveFully("b7b6");

		classification.IsCheck.Should().BeFalse();
		classification.IsMate.Should().BeFalse();
		classification.IsStalemate.Should().BeTrue();
		classification.IsResolved.Should().BeTrue();
	}
}
