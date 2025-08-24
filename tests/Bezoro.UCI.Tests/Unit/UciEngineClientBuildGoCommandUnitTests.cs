using Bezoro.UCI.Domain;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Unit;

[TestSubject(typeof(UciEngineClient))]
[Trait("Category", "Unit")]
public class UciEngineClientBuildGoCommandUnitTests
{
	[Fact]
	public void BuildGoCommand_MoveTime_TokenIncluded()
	{
		string cmd = UciEngineClient.BuildGoCommand(new() { MoveTimeMs = 1500 });
		cmd.Should().Be("go movetime 1500");
	}

	[Fact]
	public void BuildGoCommand_NodesDepthMateInfinitePonder_AllIncluded()
	{
		string cmd = UciEngineClient.BuildGoCommand(
			new()
			{
				Nodes    = 1_000_000,
				Depth    = 12,
				Mate     = 3,
				Infinite = true,
				Ponder   = true
			});

		cmd.Should().StartWith("go ");
		cmd.Should().Contain("ponder");
		cmd.Should().Contain("infinite");
		cmd.Should().Contain("nodes 1000000");
		cmd.Should().Contain("depth 12");
		cmd.Should().Contain("mate 3");
	}

	[Fact]
	public void BuildGoCommand_TimeControls_IncludeExpectedTokens()
	{
		string cmd = UciEngineClient.BuildGoCommand(
			new()
			{
				WhiteTimeMs      = 10_000,
				BlackTimeMs      = 20_000,
				WhiteIncrementMs = 100,
				BlackIncrementMs = 200
			});

		cmd.Should().StartWith("go ");
		cmd.Should().Contain("wtime 10000");
		cmd.Should().Contain("btime 20000");
		cmd.Should().Contain("winc 100");
		cmd.Should().Contain("binc 200");
		cmd.Should().NotContain("depth 6"); // since limits are present
	}

	[Fact]
	public void BuildGoCommand_WhenNoLimits_AddsDefaultDepth6()
	{
		string cmd = UciEngineClient.BuildGoCommand(new());
		cmd.Should().Be("go depth 6");
	}

	[Fact]
	public void BuildGoCommand_WhenOnlySearchMoves_DefaultDepthStillAdded()
	{
		string cmd = UciEngineClient.BuildGoCommand(
			new()
			{
				SearchMoves = new[] { "e2e4", "B1C3" }
			});

		cmd.Should().Contain("depth 6");
		cmd.Should().Contain("searchmoves e2e4 b1c3");
	}
}
