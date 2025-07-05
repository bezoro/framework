using Bezoro.UCI.API;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Helpers;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Unit;

[TestSubject(typeof(UCIConnector))]
public class BuildGoCommandUnitTests
{
	[Fact]
	public void BuildGoCommand_WithAllParameters_ReturnsCorrectlyFormattedCommand()
	{
		// Arrange
		var parameters = new SearchParameters
		{
			SearchMoves      = new[] { "e2e4", "d7d5" },
			WhiteTimeMs      = 300000,
			BlackTimeMs      = 290000,
			WhiteIncrementMs = 5000,
			BlackIncrementMs = 5000,
			Depth            = 20,
			Nodes            = 1000000,
			Mate             = 5,
			MoveTimeMs       = 10000,
			Infinite         = false
		};

		// Act
		string result = GoCommandHelper.BuildGoCommand(parameters);

		// Assert
		var expected =
			"go searchmoves e2e4 d7d5 wtime 300000 btime 290000 winc 5000 binc 5000 depth 20 nodes 1000000 mate 5 movetime 10000";

		Assert.Equal(expected, result);
	}

	[Fact]
	public void BuildGoCommand_WithDefaultParameters_ReturnsBaseCommand()
	{
		// Arrange
		var parameters = new SearchParameters();

		// Act
		string result = GoCommandHelper.BuildGoCommand(parameters);

		// Assert
		Assert.Equal("go", result);
	}

	[Fact]
	public void BuildGoCommand_WithEmptySearchMoves_DoesNotAddSearchMoves()
	{
		// Arrange
		// This tests the ?.Any() check in the implementation.
		var parameters = new SearchParameters { SearchMoves = new List<string>() };

		// Act
		string result = GoCommandHelper.BuildGoCommand(parameters);

		// Assert
		Assert.Equal("go", result);
	}

	[Fact]
	public void BuildGoCommand_WithInfiniteSearch_ReturnsCommandWithInfinite()
	{
		// Arrange
		var parameters = new SearchParameters { Infinite = true };

		// Act
		string result = GoCommandHelper.BuildGoCommand(parameters);

		// Assert
		Assert.Equal("go infinite", result);
	}

	[Fact]
	public void BuildGoCommand_WithOnlyTimeControls_ReturnsCorrectCommand()
	{
		// Arrange
		var parameters = new SearchParameters
		{
			WhiteTimeMs      = 180000,
			BlackTimeMs      = 170000,
			WhiteIncrementMs = 2000,
			BlackIncrementMs = 2000
		};

		// Act
		string result = GoCommandHelper.BuildGoCommand(parameters);

		// Assert
		Assert.Equal("go wtime 180000 btime 170000 winc 2000 binc 2000", result);
	}

	[Fact]
	public void BuildGoCommand_WithSingleSearchMove_ReturnsCorrectCommand()
	{
		// Arrange
		var parameters = new SearchParameters { SearchMoves = new[] { "g1f3" } };

		// Act
		string result = GoCommandHelper.BuildGoCommand(parameters);

		// Assert
		Assert.Equal("go searchmoves g1f3", result);
	}
}
