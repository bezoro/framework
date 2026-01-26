using Bezoro.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.API.Types;

[TestSubject(typeof(MoveAnalysis))]
public class MoveAnalysisTests
{
	[Fact]
	public void Analyze_WhenBlackQueensideCastling_SetsIsCastlingTrue()
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
	public void Analyze_WhenCapturingPiece_SetsCaptureFlagAndMovingPiece()
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
	public void Analyze_WhenPieceMissingOnFromSquare_Throws()
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
	public void Analyze_WhenWhiteKingsideCastling_SetsIsCastlingTrue()
	{
		var board = BoardState.FromFen(Fen.Default);
		board.Should().NotBeNull();

		var analysis = MoveAnalysis.Analyze("e1g1", board!.Value, MoveScore.FromCp(0), false);

		analysis.IsCastling.Should().BeTrue();
		analysis.IsNormal.Should().BeFalse();
	}
}
