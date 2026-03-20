using Xunit.Abstractions;

namespace Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;

/// <summary>
///     Base class for integration tests that require Stockfish and diagnostic logging.
///     Provides access to the shared StockfishFixture and ITestOutputHelper.
/// </summary>
public abstract class IntegrationTestBase
{
	/// <summary>
	///     Initializes a new instance of the IntegrationTestBase class.
	/// </summary>
	/// <param name="fixture">The shared Stockfish fixture.</param>
	/// <param name="output">The test output helper for diagnostic logging.</param>
	protected IntegrationTestBase(StockfishFixture fixture, ITestOutputHelper output)
	{
		Stockfish = fixture;
		Output    = output;
	}

	/// <summary>
	///     Gets the test output helper for diagnostic logging.
	/// </summary>
	protected ITestOutputHelper Output { get; }

	/// <summary>
	///     Gets the shared Stockfish fixture.
	/// </summary>
	protected StockfishFixture Stockfish { get; }

	/// <summary>
	///     Logs a timestamped message to the test output.
	/// </summary>
	/// <param name="message">The message to log.</param>
	protected void Log(string message) =>
		Output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");

	/// <summary>
	///     Logs a formatted timestamped message to the test output.
	/// </summary>
	/// <param name="format">The format string.</param>
	/// <param name="args">The format arguments.</param>
	protected void Log(string format, params object[] args) =>
		Output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {string.Format(format, args)}");
}

/// <summary>
///     Base class for unit tests that require diagnostic logging but not Stockfish.
/// </summary>
public abstract class UnitTestBase
{
	/// <summary>
	///     Initializes a new instance of the UnitTestBase class.
	/// </summary>
	/// <param name="output">The test output helper for diagnostic logging.</param>
	protected UnitTestBase(ITestOutputHelper output)
	{
		Output = output;
	}

	/// <summary>
	///     Gets the test output helper for diagnostic logging.
	/// </summary>
	protected ITestOutputHelper Output { get; }

	/// <summary>
	///     Logs a timestamped message to the test output.
	/// </summary>
	/// <param name="message">The message to log.</param>
	protected void Log(string message) =>
		Output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");

	/// <summary>
	///     Logs a formatted timestamped message to the test output.
	/// </summary>
	/// <param name="format">The format string.</param>
	/// <param name="args">The format arguments.</param>
	protected void Log(string format, params object[] args) =>
		Output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {string.Format(format, args)}");
}
