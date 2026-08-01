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
	public void ClassifyMove_WhenKingMovesTwoFilesWithoutRights_ShouldStillClassifyGeometryAsCastling()
	{
		var fen = Fen.Parse("4k3/8/8/8/8/8/8/4K3 w - - 0 1")!.Value;

		var classification = fen.ClassifyMove("e1c1");

		classification.IsQueensideCastling.Should().BeTrue();
		classification.IsKingsideCastling.Should().BeFalse();
	}

	[Fact]
	public void ClassifyMove_WhenEnPassantTargetHasNoPawn_ShouldMarkEnPassantWithoutCapture()
	{
		var fen = Fen.Parse("7k/8/8/4P3/8/8/8/K7 w - d6 0 1")!.Value;

		var classification = fen.ClassifyMove("e5d6");

		classification.IsEnPassant.Should().BeTrue();
		classification.IsCapture.Should().BeFalse();
		classification.CapturedPiece.Should().BeNull();
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
	public void ClassifyMoves_WhenKeysDifferOnlyByNormalization_ShouldPreserveOriginalDistinctKeys()
	{
		string[] moves = ["E2E4", "e2e4", "E2E4"];

		var classifications = Fen.Default.ClassifyMoves(moves);

		classifications.Should().HaveCount(2);
		classifications.Keys.Should().BeEquivalentTo("E2E4", "e2e4");
		classifications["E2E4"].Should().Be(classifications["e2e4"]);
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

	[Theory]
	[InlineData("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "e1c1")]
	[InlineData("7k/8/8/3pP3/8/8/8/K7 w - d6 0 1", "e5d6")]
	[InlineData("1r5k/P7/8/8/8/8/8/K7 w - - 0 1", "a7b8q")]
	[InlineData("7k/8/6Q1/8/8/8/8/7K w - - 0 1", "g6g7")]
	[InlineData("7k/5Q2/7K/8/8/8/8/8 w - - 0 1", "f7g7")]
	[InlineData("k7/1QK5/8/8/8/8/8/8 w - - 0 1", "b7b6")]
	public void ClassifyMovesFully_WhenPositionContainsRepresentativeMove_ShouldMatchEverySingleClassification(
		string rawFen,
		string representativeMove)
	{
		var fen = Fen.Parse(rawFen)!.Value;
		var legalMoves = fen.GetLegalMoves();

		legalMoves.Should().Contain(representativeMove);
		legalMoves.Length.Should().BeGreaterThan(1);
		var batch = fen.ClassifyMovesFully(legalMoves);

		batch.Should().HaveCount(legalMoves.Length);
		foreach (string move in legalMoves)
			batch[move].Should().Be(fen.ClassifyMoveFully(move));
	}

	[Fact]
	public void ClassifyMovesFully_WhenMultipleMovesAreProvided_ShouldMatchEachSingleClassification()
	{
		string[] moves = ["e2e4", "g1f3", "b1c3"];

		var batch = Fen.Default.ClassifyMovesFully(moves);

		batch.Should().HaveCount(moves.Length);
		foreach (string move in moves)
			batch[move].Should().Be(Fen.Default.ClassifyMoveFully(move));
	}

	[Fact]
	public void ClassifyMoveFully_WhenMoveCapturesDefendingKing_ShouldReportStalemate()
	{
		var fen = Fen.Parse("7k/6Q1/8/8/8/8/8/K7 w - - 0 1")!.Value;

		var classification = fen.ClassifyMoveFully("g7h8");

		classification.IsCapture.Should().BeTrue();
		classification.CapturedPiece.Should().Be('k');
		classification.IsCheck.Should().BeFalse();
		classification.IsMate.Should().BeFalse();
		classification.IsStalemate.Should().BeTrue();
		classification.IsResolved.Should().BeTrue();
	}

	[Theory]
	[InlineData("", "Move must not be blank. (Parameter 'move')")]
	[InlineData("e2e9", "Move must be valid UCI notation. (Parameter 'move')")]
	[InlineData("e3e4", "No piece exists on source square 'e3'. (Parameter 'move')")]
	public void ClassifyMove_WhenMoveIsInvalid_ShouldThrowExactArgumentException(string move, string message)
	{
		Action act = () => Fen.Default.ClassifyMove(move);

		var exception = act.Should().ThrowExactly<ArgumentException>().Which;
		exception.Message.Should().Be(message);
	}
}
