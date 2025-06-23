// In your test project

using JetBrains.Annotations;

namespace Bezoro.UCI.Tests;

[TestSubject(typeof(UCIConnector))]
public class UCIConnectorTests : IAsyncLifetime
{
	private const string        StockfishPath = "Engine/stockfish/stockfish-windows-x86-64-avx2.exe";
	private       UCIConnector? _connector;

	public async Task InitializeAsync()
	{
		_connector = new UCIConnector(StockfishPath);
		await _connector.StartEngineAsync();
	}

	public async Task DisposeAsync()
	{
		if (_connector != null)
		{
			await _connector.DisposeAsync();
		}
	}

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
		await _connector!.SetPositionAsync();

		// Act: Give the engine 100ms to think.
		string bestMove = await _connector.GetBestMoveAsync(TimeSpan.FromMilliseconds(100));

		// Assert
		// We can't know the exact best move, but we can check if it's a valid move format.
		Assert.NotNull(bestMove);
		Assert.True(UCIHelper.IsValidUciMove(bestMove));
	}

	[Fact]
	public async Task GetLegalMovesAsync_FromStartPosition_ShouldReturn20Moves()
	{
		// Arrange
		await _connector!.SetPositionAsync();

		// Act
		List<string> legalMoves = await _connector.GetLegalMovesAsync();

		// Assert
		Assert.NotNull(legalMoves);
		Assert.Equal(20, legalMoves.Count);
	}

	[Fact]
	public async Task IsMoveLegalAsync_WithValidAndInvalidMoves_ReturnsCorrectResult()
	{
		// Arrange
		await _connector!.SetPositionAsync();

		// Act
		bool isE2E4Legal = await _connector.IsMoveLegalAsync("e2e4");
		bool isE2E5Legal = await _connector.IsMoveLegalAsync("e2e5");

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
		Exception? exception = await Record.ExceptionAsync(() => _connector!.SetOptionAsync("Threads", "2"));

		// Assert
		Assert.Null(exception);
	}

	[Fact]
	public async Task SetPositionAsync_WhenGivenValidPosition_ShouldSetPosition()
	{
		// Arrange
		// This sequence of moves (1. e4 e5) leads to a well-known open position.
		var moves = new[] { "e2e4", "e7e5" };
		await _connector!.SetPositionAsync("startpos", moves);

		// Act
		// Ask the engine for the best move in this position.
		string bestMove = await _connector.GetBestMoveAsync(TimeSpan.FromMilliseconds(100));

		// Assert
		// If we get a valid move, it means the engine processed the position correctly.
		Assert.NotNull(bestMove);
		Assert.True(UCIHelper.IsValidUciMove(bestMove));
	}

	[Fact]
	public void StartAsync_ShouldPopulateEngineInfo()
	{
		// Assert
		Assert.NotNull(_connector!.EngineInfo);
		Assert.Contains(_connector.EngineInfo, line => line.Contains("id name Stockfish"));
		Assert.Contains(_connector.EngineInfo, line => line.Contains("id author"));
	}

	[Fact]
	public async Task StartEngineAsync_WhenEnginePathIsValid_ShouldStartEngine()
	{
		// Arrange
		await using var connector = new UCIConnector(StockfishPath);

		// Act
		await connector.StartEngineAsync();

		// Assert
		Assert.NotNull(connector.EngineInfo);
		Assert.NotEmpty(connector.EngineInfo);
	}

	[Fact]
	public async Task StopAsync_WhenEngineIsRunning_ShouldStopEngine()
	{
		// Arrange
		// Use a new connector instance to ensure test isolation.
		// `await using` ensures it's disposed correctly at the end of the test.
		await using var connector = new UCIConnector(StockfishPath);
		await connector.StartEngineAsync();

		// Act
		// Stop the engine process.
		await connector.StopEngineAsync();

		// Assert
		// After stopping, any attempt to communicate with the engine should fail
		// because the underlying process and its communication streams are closed.
		// We expect an ObjectDisposedException, as the resources should be cleaned up.
		await Assert.ThrowsAsync<IOException>(() => connector.GetLegalMovesAsync());
	}
}
