using Bezoro.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.API.Types;

[TestSubject(typeof(MoveAnalysis))]
public class MoveAnalysisTests
{
	[Fact]
	public void Analyze_WhenWhiteKingsideCastling_SetsIsCastlingTrue()
	{
		var board = BoardState.FromFen(Fen.Default);
		board.Should().NotBeNull();

		var analysis = MoveAnalysis.Analyze("e1g1", board!.Value, MoveScore.FromCp(0), false);

		analysis.IsCastling.Should().BeTrue();
		analysis.IsNormal.Should().BeFalse();
	}

	[Fact]
	public void Analyze_WhenBlackQueensideCastling_SetsIsCastlingTrue()
	{
		var fen   = Fen.Parse("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR b KQkq - 0 1");
		fen.Should().NotBeNull();

		var board  = BoardState.FromFen(fen!.Value);
		board.Should().NotBeNull();

		var result = MoveAnalysis.Analyze("e8c8", board!.Value, MoveScore.FromCp(0), false);

		result.IsCastling.Should().BeTrue();
		result.IsNormal.Should().BeFalse();
	}
}

