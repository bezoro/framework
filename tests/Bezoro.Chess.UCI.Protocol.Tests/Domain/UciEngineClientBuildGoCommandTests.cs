using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientBuildGoCommandTests
{
	[Fact]
	public void BuildGoCommand_WhenMovesToGoIsNotPositive_ShouldOmitMovesToGo()
	{
		string cmd = UciEngineClient.BuildGoCommand(
			new() { WhiteTimeMs = 1000, BlackTimeMs = 2000, MovesToGo = 0 }
		);

		cmd.Should().Be(
			"go wtime 1000 btime 2000",
			"movestogo should only be sent when the value is strictly positive"
		);
	}

	[Fact]
	public void BuildGoCommand_WhenMovesToGoIsPositive_ShouldIncludeMovesToGoBeforeSearchMoves()
	{
		string cmd = UciEngineClient.BuildGoCommand(
			new() { WhiteTimeMs = 1000, BlackTimeMs = 2000, MovesToGo = 30, SearchMoves = ["E2E4"] }
		);

		cmd.Should().Be(
			"go wtime 1000 btime 2000 movestogo 30 searchmoves e2e4",
			"movestogo should be emitted for positive values and searchmoves should remain last"
		);
	}

	[Fact]
	public void BuildGoCommand_WhenNodesDepthAndMateAreProvided_ShouldIncludeAllLimits()
	{
		string cmd = UciEngineClient.BuildGoCommand(new() { Nodes = 123, Depth = 7, Mate = 2 });
		cmd.Should().Be("go nodes 123 depth 7 mate 2", "nodes, depth, and mate should be included");
	}

	[Fact]
	public void BuildGoCommand_WhenNoLimitsAreProvided_ShouldThrowArgumentException()
	{
		var act = () => UciEngineClient.BuildGoCommand(new());

		act.Should().Throw<ArgumentException>()
		   .WithMessage("*At least one explicit go limit*");
	}

	[Fact]
	public void BuildGoCommand_WhenPonderAndInfiniteAreEnabled_ShouldCombineFlags()
	{
		string cmd = UciEngineClient.BuildGoCommand(new() { Ponder = true, Infinite = true });
		cmd.Should().Be("go ponder infinite", "ponder and infinite flags should be combined");
	}

	[Fact]
	public void BuildGoCommand_WhenSearchMovesAreProvided_ShouldFilterAndLowercaseMoves()
	{
		var act = () => UciEngineClient.BuildGoCommand(
			new() { SearchMoves = ["E2E4", "bad", "a7a8Q", ""] }
		);

		act.Should().Throw<ArgumentException>()
		   .WithMessage("*At least one explicit go limit*");
	}

	[Fact]
	public void BuildGoCommand_WhenTimeControlsAreProvided_ShouldFormatCommandCorrectly()
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
	public void BuildGoCommand_WhenDepthIsZero_ShouldThrowArgumentOutOfRangeException()
	{
		var act = () => UciEngineClient.BuildGoCommand(new() { Depth = 0 });

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void BuildGoCommand_WhenMoveTimeIsNotPositive_ShouldThrowArgumentOutOfRangeException()
	{
		var act = () => UciEngineClient.BuildGoCommand(new() { MoveTimeMs = 0 });

		act.Should().Throw<ArgumentOutOfRangeException>();
	}
}
