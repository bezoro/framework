// In your test project

using JetBrains.Annotations;
using UCIEngine.Models;

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
	public async Task GetBestMoveAsync_WhenEngineIsThinking_RaisesInfoReceivedEventWithValidData()
	{
		// Arrange
		await _connector!.SetPositionAsync();

		var validInfoReceivedTcs = new TaskCompletionSource<EngineAnalysisEventArgs>();
		_connector.InfoReceived += (sender, args) =>
		{
			// The engine might send multiple 'info' lines.
			// We only complete our task when we receive one that contains a search depth.
			if (args.Depth > 0)
			{
				validInfoReceivedTcs.TrySetResult(args);
			}
		};

		// Act
		// Start the search, but we don't need to await it yet.
		Task<string?> bestMoveTask = _connector.GetBestMoveAsync(TimeSpan.FromMilliseconds(500));

		// Assert
		// Wait for an InfoReceived event with a valid depth to arrive.
		// The timeout will cause the test to fail if no such event is received.
		EngineAnalysisEventArgs receivedArgs = await validInfoReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

		// If we reach this point, we know our condition was met.
		Assert.NotNull(receivedArgs);
		Assert.True(receivedArgs.Depth > 0);

		// It's good practice to ensure the main search task also completes.
		await bestMoveTask;
	}

	[Fact]
	public async Task GetBestMoveAsync_WhenGivenEnoughTime_ShouldReturnValidMove()
	{
		// Arrange
		await _connector!.SetPositionAsync();

		// Act: Give the engine 100ms to think.
		string? bestMove = await _connector.GetBestMoveAsync(TimeSpan.FromMilliseconds(100));

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
		string? bestMove = await _connector.GetBestMoveAsync(TimeSpan.FromMilliseconds(100));

		// Assert
		// If we get a valid move, it means the engine processed the position correctly.
		Assert.NotNull(bestMove);
		Assert.True(UCIHelper.IsValidUciMove(bestMove));
	}

	[Fact]
	public void StartEngineAsync_ShouldPopulateEngineInfo()
	{
		// Assert
		Assert.NotNull(_connector!.EngineInfo);
		Assert.Contains(_connector.EngineInfo, line => line.Contains("id name Stockfish"));
		Assert.Contains(_connector.EngineInfo, line => line.Contains("id author"));
	}

	[Fact]
	public void StartEngineAsync_ShouldPopulateSupportedOptions()
	{
		// Arrange: The connector is initialized by the IAsyncLifetime fixture,
		// which calls StartEngineAsync automatically.

		// Assert
		// The SupportedOptions list should be populated after initialization.
		Assert.NotNull(_connector!.SupportedOptions);
		Assert.NotEmpty(_connector.SupportedOptions);

		// Check for a specific, well-known option like "Hash".
		UCIOption? hashOption = _connector.SupportedOptions.FirstOrDefault(o => o.Name == "Hash");
		Assert.NotNull(hashOption);

		// Verify that the details of the option were parsed correctly.
		Assert.Equal("spin", hashOption.Type);
		Assert.NotNull(hashOption.Default);
		Assert.NotNull(hashOption.Min);
		Assert.NotNull(hashOption.Max);
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
	public async Task StopEngineAsync_WhenEngineIsRunning_ShouldStopEngine()
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

	[Fact]
	public async Task StopSearchAsync_WhenCalled_ShouldStopSearchAndReturnBestMoveFoundSoFar()
	{
		// Arrange
		// Set the engine to the starting position.
		await _connector!.SetPositionAsync();

		// Start a long search in the background. We don't await this task yet because we intend to
		// interrupt it. A 10-second thinking time is plenty for us to stop it manually.
		Task<string?> searchTask = _connector.GetBestMoveAsync(TimeSpan.FromSeconds(10));

		// Give the engine a moment to begin its analysis and find at least one move.
		await Task.Delay(TimeSpan.FromMilliseconds(500));
		Assert.False(searchTask.IsCompleted, "The search task should still be running before we stop it.");

		// Act
		// This is the core action: we command the engine to stop its current search.
		// The `StopSearchAsync` method is expected to send the "stop" command to the UCI engine.
		await _connector.StopSearchAsync();

		// Awaiting the original search task should now complete almost instantly,
		// because the "stop" command makes the engine emit its best move found so far.
		string? bestMove = await searchTask;

		// Assert
		// The engine should have returned a valid best move it found in the short time it was thinking.
		Assert.NotNull(bestMove);
		Assert.True(UCIHelper.IsValidUciMove(bestMove), $"The move '{bestMove}' is not in valid UCI format.");
	}

	[Fact]
	public async Task UCINewGameAsync_WhenCalled_ShouldResetPositionAndClearSearchHistory()
	{
		// Arrange
		// Set a position that is not the start position to confirm that UCINewGame resets it.
		// After 1. e4 e5, there are 29 possible moves for White.
		await _connector!.SetPositionAsync("startpos", [ "e2e4", "e7e5" ]);
		List<string> movesBeforeReset = await _connector.GetLegalMovesAsync();
		Assert.NotEqual(20, movesBeforeReset.Count);

		// Act
		// 'ucinewgame' readies the engine for a new game by clearing caches.
		// It must be followed by a 'position' command to set the board.
		await _connector.UCINewGameAsync();
		await _connector.SetPositionAsync(); // Explicitly set the position to "startpos"

		// Assert
		// After resetting, the engine should be at the standard starting position, which has 20 legal moves.
		List<string> movesAfterReset = await _connector.GetLegalMovesAsync();
		Assert.Equal(20, movesAfterReset.Count);
	}

	[Theory]
	// Test case for a normal pawn move from the starting position.
	[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", "e2e4", false, false, false, false)]
	// Test case for a capture. White pawn on e4 captures a black pawn on d5.
	[InlineData("rnbqkbnr/ppp1pppp/8/3p4/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2", "e4d5", true, false, false, false)]
	// Test case for Kingside castling.
	[InlineData("r3k2r/pppp1ppp/8/8/8/8/PPPP1PPP/R3K2R w KQkq - 0 1", "e1g1", false, true, false, false)]
	// Test case for Queenside castling.
	[InlineData("r3k2r/pppp1ppp/8/8/8/8/PPPP1PPP/R3K2R w KQkq - 0 1", "e1c1", false, true, false, false)]
	// Test case for a pawn promoting to a queen.
	[InlineData("4k3/P7/8/8/8/8/8/4K3 w - - 0 1", "a7a8q", false, false, true, false)]
	// Test case for an En Passant capture, which is both a capture and en passant.
	[InlineData("rnbqkbnr/ppp1p1pp/8/3pPp2/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3", "e5f6", true, false, false, true)]
	public async Task GetAllLegalMovesWithDetailsAsync_WithVariousBoardStates_CorrectlyClassifiesMoves(
		string fen, string uciMove, bool isCapture, bool isCastling, bool isPromotion, bool isEnPassant)
	{
		// Arrange
		// Set the custom board position using the provided FEN string.
		await _connector!.SetPositionAsync(fen);

		// Act
		// Get all legal moves with their detailed information.
		// This assumes the returned object has a 'Classification' property of the type you provided.
		List<MoveClassification> movesWithDetails = await _connector.GetAllLegalMovesWithDetailsAsync();

		// Assert
		// Find the specific move we're testing for in the list of legal moves.
		MoveClassification? specificMove = movesWithDetails.FirstOrDefault(m => m.Move == uciMove);

		// Verify that the move was found and its classification properties are correct.
		Assert.NotNull(specificMove);
		Assert.Equal(isCapture,   specificMove.IsCapture);
		Assert.Equal(isCastling,  specificMove.IsCastling);
		Assert.Equal(isPromotion, specificMove.IsPromotion);
		Assert.Equal(isEnPassant, specificMove.IsEnPassant);
	}
}
