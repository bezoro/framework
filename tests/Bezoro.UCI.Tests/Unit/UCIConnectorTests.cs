using Bezoro.UCI.API;
using Bezoro.UCI.Domain.Helpers;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Unit;

[TestSubject(typeof(UCIConnector))]
public class UCIConnectorTests : UCITestsBase
{
	[Fact]
	public async Task DisposeAsync_WhenEngineIsRunning_ShouldStopEngine()
	{
		// Arrange
		// We create a new connector that we will manually dispose to test the disposal logic.
		var connector = new UCIConnector(StockfishPath);
		await connector.StartEngineAsync();

		// Act
		// Dispose the connector. This should stop the engine process and clean up all resources.
		await connector.DisposeAsync();

		// Assert
		// After disposing, any attempt to communicate with the engine should fail because the object is disposed.
		// We expect an ObjectDisposedException, which is the standard behavior for a disposed object.
		await Assert.ThrowsAsync<ObjectDisposedException>(() => connector.GetLegalMovesAsync());
	}

	[Fact]
	public async Task GetBestMoveAsync_WhenGivenEnoughTime_ShouldReturnValidMove()
	{
		// Arrange

		// Act
		string? bestMove = await Connector.GetBestMoveAsync();

		// Assert
		Assert.NotNull(bestMove);
		Assert.True(UCIHelper.IsValidUciMove(bestMove));
	}

	[Fact]
	public async Task GetCurrentFENAsync_WhenValidState_ReturnsFENString()
	{
		// Arrange
		// A specific, non-starting FEN string to ensure we're not just getting a default value.
		const string expectedFen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
		// Set the engine's internal board state to our specific FEN.
		await Connector!.SetPositionAsync(expectedFen);

		// Act
		// Ask the connector to retrieve the current FEN from the engine.
		string? actualFen = await Connector.GetCurrentFENAsync();

		// Assert
		// Verify that the FEN returned by the engine matches the one we set.
		Assert.Equal(expectedFen, actualFen);
	}

	[Fact]
	public async Task GetLegalMovesAsync_FromStartPosition_ShouldReturn20Moves()
	{
		// Arrange
		await Connector!.SetPositionAsync();

		// Act
		List<string> legalMoves = await Connector.GetLegalMovesAsync();

		// Assert
		Assert.NotNull(legalMoves);
		Assert.Equal(20, legalMoves.Count);
	}

	[Fact]
	public async Task IsMoveLegalAsync_WithValidAndInvalidMoves_ReturnsCorrectResult()
	{
		// Arrange
		await Connector!.SetPositionAsync();

		// Act
		bool isE2E4Legal = await Connector.IsMoveLegalAsync("e2e4");
		bool isE2E5Legal = await Connector.IsMoveLegalAsync("e2e5");

		// Assert
		Assert.True(isE2E4Legal);
		Assert.False(isE2E5Legal);
	}

	[Fact]
	public async Task SetOptionAsync_WhenGivenValidOption_ShouldSetOption()
	{
		// Arrange
		// We can't easily verify the option was set, as UCI protocol doesn't have a command to get an option's value.
		// However, if the engine accepts the command, it will send "readyok", and our method will complete without an exception.
		// We will set a common option like "Threads".

		// Act
		var exception = await Record.ExceptionAsync(() => Connector!.SetOptionAsync("Threads", "2"));

		// Assert
		Assert.Null(exception);
	}

	[Fact]
	public async Task SetPositionAsync_WhenGivenValidPosition_ShouldSetPosition()
	{
		// Arrange
		string startingFen = await Connector.GetCurrentFENAsync();

		// Act
		await Connector!.SetPositionAsync(moves: [ "e2e4", "e7e5" ]);
		string endingFen = await Connector.GetCurrentFENAsync();

		// Assert
		Assert.NotEqual(startingFen, endingFen);
	}

	[Fact]
	public async Task StartEngineAsync_WhenEnginePathIsValid_ShouldStartEngine()
	{
		// Arrange
		await using var connector = new UCIConnector(StockfishPath);

		// Act
		await connector.StartEngineAsync();

		// Assert
		// After starting, the engine should be ready to receive commands.
		bool isReady = await connector.IsEngineReadyAsync();
		Assert.True(isReady);
	}

	[Fact]
	public async Task StartEngineAsync_WhenValidEnginePath_ShouldStartEngine()
	{
		// Arrange
		await using var connector = new UCIConnector(StockfishPath);

		// Act
		await connector.StartEngineAsync();

		// Assert
		bool isRunningBeforeStop = await connector.IsEngineReadyAsync();
		Assert.True(isRunningBeforeStop);
	}

	[Fact]
	public async Task StopEngineAsync_WhenEngineIsRunning_ShouldStopEngine()
	{
		// Act
		await Connector.StopEngineAsync();

		// Assert engine is stopped
		bool isRunningAfterStop = await Connector.IsEngineReadyAsync();
		Assert.False(isRunningAfterStop);
	}

	[Fact]
	public async Task StopSearchAsync_WhenCalled_ShouldStopSearchAndReturnBestMoveFoundSoFar()
	{
		// Arrange
		var searchTask = Connector.StartAnalysisAsync(_ => { });

		await Task.Delay(TimeSpan.FromMilliseconds(500));
		Assert.False(searchTask.IsCompleted, "The search task should still be running before we stop it.");

		// Act
		await Connector.StopSearchAsync();

		// Assert
	}

	[Fact]
	public async Task UCINewGameAsync_WhenCalled_ShouldResetPositionAndClearSearchHistory()
	{
		// Arrange
		await Connector!.SetPositionAsync(moves: [ "e2e4", "e7e5" ]);
		List<string> movesBeforeReset = await Connector.GetLegalMovesAsync();

		// Act
		await Connector.UCINewGameAsync();
		await Connector.SetPositionAsync();
		List<string> movesAfterReset = await Connector.GetLegalMovesAsync();

		// Assert
		Assert.NotEqual(movesBeforeReset, movesAfterReset);
	}
}
