using Bezoro.Chess.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Tests.API.Types;

[TestSubject(typeof(MoveAnalysis))]
public class MoveAnalysisTests
{
	[Fact]
	public void Analyze_WhenBlackQueensideCastling_ShouldSetIsCastlingTrue()
	{
		var fen = Fen.Parse("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR b KQkq - 0 1");
		fen.Should().NotBeNull();

		var board = BoardState.FromFen(fen!.Value);
		board.Should().NotBeNull();

		var result = MoveAnalysis.Analyze("e8c8", board!.Value, MoveScore.FromCp(0), false);

		result.IsCastling.Should().BeTrue();
		result.IsNormal.Should().BeFalse();
	}

	[Fact]
	public void Analyze_WhenCapturingPiece_ShouldSetCaptureFlagAndMovingPiece()
	{
		var captureFen = Fen.Parse("rnbqkbnr/pppppppp/3P4/8/8/8/PPP1PPPP/RNBQKBNR b KQkq - 0 1");
		captureFen.Should().NotBeNull();

		var board = BoardState.FromFen(captureFen!.Value);
		board.Should().NotBeNull();

		var analysis = MoveAnalysis.Analyze("e7d6", board!.Value, MoveScore.FromCp(0), false);

		analysis.IsCapture.Should().BeTrue();
		analysis.MovingPiece.Should().NotBeNull();
		analysis.MovingPiece!.Value.Char.Should().Be('p');
		analysis.IsNormal.Should().BeFalse();
	}

	[Fact]
	public void Analyze_WhenSourceSquareHasNoPiece_ShouldThrowArgumentException()
	{
		var afterE4 = Fen.Parse("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1");
		afterE4.Should().NotBeNull();

		var board = BoardState.FromFen(afterE4!.Value);
		board.Should().NotBeNull();

		var act = () => MoveAnalysis.Analyze("e2e4", board!.Value, MoveScore.FromCp(0), false);

		act.Should().Throw<ArgumentException>()
		   .Where(ex => ex.Message.Contains("No piece found on square 'e2' for move 'e2e4'."));
	}

	[Fact]
	public void Analyze_WhenWhiteKingsideCastling_ShouldSetIsCastlingTrue()
	{
		var board = BoardState.FromFen(Fen.Default);
		board.Should().NotBeNull();

		var analysis = MoveAnalysis.Analyze("e1g1", board!.Value, MoveScore.FromCp(0), false);

		analysis.IsCastling.Should().BeTrue();
		analysis.IsNormal.Should().BeFalse();
	}

	[Fact]
	public void Analyze_WhenMoveProducesCheckmateWithoutMateScore_ShouldSetCheckAndMateTrue()
	{
		var fen = Fen.Parse("7k/5Q2/7K/8/8/8/8/8 w - - 0 1");
		fen.Should().NotBeNull();

		var board = BoardState.FromFen(fen!.Value);
		board.Should().NotBeNull();

		var analysis = MoveAnalysis.Analyze("f7g7", board!.Value, MoveScore.FromCp(0), false);

		analysis.IsCheck.Should().BeTrue();
		analysis.IsMate.Should().BeTrue();
	}

	[Fact]
	public void Analyze_WhenMoveProducesStalemateWithoutExternalFlag_ShouldSetIsStalemateTrue()
	{
		var fen = Fen.Parse("k7/1QK5/8/8/8/8/8/8 w - - 0 1");
		fen.Should().NotBeNull();

		var board = BoardState.FromFen(fen!.Value);
		board.Should().NotBeNull();

		var analysis = MoveAnalysis.Analyze("b7b6", board!.Value, MoveScore.FromCp(0), false);

		analysis.IsStalemate.Should().BeTrue();
	}
}
