using Bezoro.UCI.Domain;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientBuildGoCommandTests
{
	[Fact]
	public void ShouldAddDefaultDepthWhenNoLimits()
	{
		string cmd = UciEngineClient.BuildGoCommand(new());
		cmd.Should().Be("go depth 6", "default depth should be 6");
	}

	[Fact]
	public void ShouldCombinePonderAndInfiniteFlags()
	{
		string cmd = UciEngineClient.BuildGoCommand(new() { Ponder = true, Infinite = true });
		cmd.Should().Be("go ponder infinite", "ponder and infinite flags should be combined");
	}

	[Fact]
	public void ShouldFilterAndLowercaseSearchmoves()
	{
		string cmd = UciEngineClient.BuildGoCommand(
			new() { SearchMoves = ["E2E4", "bad", "a7a8Q", ""] }
		);

		cmd.Should().Be("go depth 6 searchmoves e2e4 a7a8q", "searchmoves should be filtered and lowercased");
	}

	[Fact]
	public void ShouldFormatTimeControlsCorrectly()
	{
		string cmd = UciEngineClient.BuildGoCommand(
			new() { WhiteTimeMs = 1000, BlackTimeMs = 2000, WhiteIncrementMs = 10, BlackIncrementMs = 20 }
		);

		cmd.Should().Be(
			"go wtime 1000 btime 2000 winc 10 binc 20",
			"time controls should be formatted correctly"
		);
	}

	[Fact]
	public void ShouldIncludeNodesDepthMate()
	{
		string cmd = UciEngineClient.BuildGoCommand(new() { Nodes = 123, Depth = 7, Mate = 2 });
		cmd.Should().Be("go nodes 123 depth 7 mate 2", "nodes, depth, and mate should be included");
	}
}
