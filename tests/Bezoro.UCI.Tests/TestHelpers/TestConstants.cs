namespace Bezoro.UCI.Tests.TestHelpers;

/// <summary>
/// Centralized constants for UCI tests to eliminate magic numbers and improve maintainability.
/// </summary>
public static class TestConstants
{
	#region Timeouts

	/// <summary>
	/// Default timeout for test operations (5 seconds).
	/// </summary>
	public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Short timeout for quick operations (1.5 seconds).
	/// </summary>
	public static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(1500);

	/// <summary>
	/// Medium timeout for moderate operations (3 seconds).
	/// </summary>
	public static readonly TimeSpan MediumTimeout = TimeSpan.FromMilliseconds(3000);

	/// <summary>
	/// Long timeout for complex operations (4 seconds).
	/// </summary>
	public static readonly TimeSpan LongTimeout = TimeSpan.FromMilliseconds(4000);

	/// <summary>
	/// Extended timeout for slow operations (8 seconds).
	/// </summary>
	public static readonly TimeSpan ExtendedTimeout = TimeSpan.FromSeconds(8);

	/// <summary>
	/// Very short timeout for cancellation tests (50 milliseconds).
	/// </summary>
	public static readonly TimeSpan CancellationTimeout = TimeSpan.FromMilliseconds(50);

	/// <summary>
	/// Tiny timeout for write operations (10 milliseconds).
	/// </summary>
	public static readonly TimeSpan TinyTimeout = TimeSpan.FromMilliseconds(10);

	/// <summary>
	/// Small timeout for quick writes (100 milliseconds).
	/// </summary>
	public static readonly TimeSpan SmallTimeout = TimeSpan.FromMilliseconds(100);

	#endregion

	#region Delays

	/// <summary>
	/// Very short delay for async operations (5 milliseconds).
	/// </summary>
	public static readonly TimeSpan VeryShortDelay = TimeSpan.FromMilliseconds(5);

	/// <summary>
	/// Short delay for async operations (10 milliseconds).
	/// </summary>
	public static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(10);

	/// <summary>
	/// Medium delay for async operations (50 milliseconds).
	/// </summary>
	public static readonly TimeSpan MediumDelay = TimeSpan.FromMilliseconds(50);

	/// <summary>
	/// Standard delay for async operations (100 milliseconds).
	/// </summary>
	public static readonly TimeSpan StandardDelay = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Longer delay for async operations (200 milliseconds).
	/// </summary>
	public static readonly TimeSpan LongerDelay = TimeSpan.FromMilliseconds(200);

	/// <summary>
	/// Delay for backpressure tests (500 milliseconds).
	/// </summary>
	public static readonly TimeSpan BackpressureDelay = TimeSpan.FromMilliseconds(500);

	#endregion

	#region Test Data

	/// <summary>
	/// Expected number of legal moves from the starting position (20).
	/// </summary>
	public const int ExpectedStartingPositionMoves = 20;

	/// <summary>
	/// Default depth for go commands (6).
	/// </summary>
	public const int DefaultGoDepth = 6;

	/// <summary>
	/// Number of iterations for handshake pump loops (20-50).
	/// </summary>
	public const int HandshakePumpIterations = 20;

	/// <summary>
	/// Extended number of iterations for handshake pump loops (50).
	/// </summary>
	public const int ExtendedHandshakePumpIterations = 50;

	#endregion

	#region Common FEN Strings

	/// <summary>
	/// Standard starting position FEN.
	/// </summary>
	public const string StandardFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

	/// <summary>
	/// FEN after e2e4 (black to move).
	/// </summary>
	public const string AfterE2E4Fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1";

	/// <summary>
	/// FEN for white mate in one position.
	/// </summary>
	public const string WhiteMateInOneFen = "7k/5Q2/7K/8/8/8/8/8 w - - 0 1";

	/// <summary>
	/// FEN for stalemate position.
	/// </summary>
	public const string StalemateFen = "k7/1QK5/8/8/8/8/8/8 w - - 0 1";

	/// <summary>
	/// FEN for Italian game position.
	/// </summary>
	public const string ItalianGameFen = "r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 2 3";

	#endregion

	#region Common Moves

	/// <summary>
	/// Common opening moves from starting position.
	/// </summary>
	public static readonly string[] CommonOpeningMoves = { "e2e4", "d2d4", "g1f3", "c2c4" };

	/// <summary>
	/// Common black responses to e2e4.
	/// </summary>
	public static readonly string[] CommonBlackResponses = { "e7e5", "c7c5" };

	#endregion

	#region UCI Commands

	/// <summary>
	/// UCI initialization command.
	/// </summary>
	public const string UciCommand = "uci";

	/// <summary>
	/// UCI ready command.
	/// </summary>
	public const string IsReadyCommand = "isready";

	/// <summary>
	/// UCI position display command.
	/// </summary>
	public const string PositionDisplayCommand = "d";

	/// <summary>
	/// UCI perft command template.
	/// </summary>
	public const string GoPerftCommand = "go perft 1";

	#endregion

	#region Channel Configuration

	/// <summary>
	/// Small channel capacity for backpressure tests (1).
	/// </summary>
	public const int SmallChannelCapacity = 1;

	/// <summary>
	/// Quit grace period for process shutdown (100 milliseconds).
	/// </summary>
	public static readonly TimeSpan QuitGracePeriod = TimeSpan.FromMilliseconds(100);

	#endregion
}

