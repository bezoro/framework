using Bezoro.Chess.UCI.Protocol.API.Types;
using Bezoro.Chess.UCI.Protocol.ConsoleDemo;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.ConsoleDemo;

[TestSubject(typeof(MoveHistoryFormatter))]
public class MoveHistoryFormatterTests
{
	[Fact]
	public void BuildLines_WhenHistoryIsEmpty_ShouldReturnEmptyMessage()
	{
		string[] lines = MoveHistoryFormatter.BuildLines([], static _ => null);

		lines.Should().Equal("No moves have been played yet.");
	}

	[Fact]
	public void BuildLines_WhenAnalysisExists_ShouldFormatPlayerRelativeCpPerMove()
	{
		MoveHistoryEntry[] history =
		[
			new(1, 'w', "e2e4", "fen-0", "fen-1"),
			new(1, 'b', "e7e5", "fen-1", "fen-2")
		];

		var scores = new Dictionary<string, PositionScore>
		{
			["e2e4"] = new(17, null),
			["e7e5"] = new(5, null)
		};

		string[] lines = MoveHistoryFormatter.BuildLines(
			history,
			entry => scores.TryGetValue(entry.Move, out var score) ? score : null
		);

		lines.Should().Equal(
			"Move history:",
			"  1. e2e4    +17 cp",
			"  1... e7e5  +5 cp"
		);
	}

	[Fact]
	public void BuildLines_WhenAnalysisIsMissing_ShouldShowAnalyzingPlaceholder()
	{
		MoveHistoryEntry[] history =
		[
			new(2, 'w', "g1f3", "fen-2", "fen-3")
		];

		string[] lines = MoveHistoryFormatter.BuildLines(history, static _ => null);

		lines.Should().Equal(
			"Move history:",
			"  2. g1f3    Analyzing..."
		);
	}
}
