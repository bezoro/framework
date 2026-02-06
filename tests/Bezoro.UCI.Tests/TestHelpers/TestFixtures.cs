namespace Bezoro.UCI.Tests.TestHelpers;

/// <summary>
///     Collection definition for tests that require Stockfish.
///     Tests in this collection share a single StockfishFixture instance.
/// </summary>
[CollectionDefinition("Stockfish")]
public class StockfishCollection : ICollectionFixture<StockfishFixture>;

/// <summary>
///     Shared test fixtures for UCI tests to improve test isolation and reduce duplication.
/// </summary>
public class StockfishFixture : IAsyncLifetime
{
	/// <summary>
	///     Gets the path to the Stockfish executable.
	/// </summary>
	public string StockfishPath { get; } = TestResourcePaths.STOCKFISH_PATH;

	public Task InitializeAsync()
	{
		// Verify Stockfish is available
		if (!IsStockfishAvailable())
			throw new InvalidOperationException(
				$"Stockfish not found at path: {StockfishPath}. " +
				"Ensure the Stockfish executable is available for integration tests."
			);

		return Task.CompletedTask;
	}

	public Task DisposeAsync() =>
		// No cleanup needed for fixture
		Task.CompletedTask;

	// Note: ProcessUciTransport is internal, so tests should create it directly
	// This fixture primarily provides the Stockfish path and availability check

	/// <summary>
	///     Verifies that Stockfish is available at the configured path.
	/// </summary>
	public bool IsStockfishAvailable() => File.Exists(StockfishPath);
}

/// <summary>
///     Helper methods for creating mock transports in unit tests.
/// </summary>
public static class TransportFixture
{
	/// <summary>
	///     Creates a mock transport that can be configured for unit tests.
	///     This is a placeholder for NSubstitute-based mock creation helpers.
	/// </summary>
	public static class Mock
	{
		// Note: Actual mock creation should use NSubstitute in test classes
		// This class serves as a namespace for future mock helpers if needed
	}
}
