namespace Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;

/// <summary>
///     Centralized constants for UCI tests to eliminate magic numbers and improve maintainability.
/// </summary>
public static class TestConstants
{
	/// <summary>
	///     Default depth for go commands (6).
	/// </summary>
	public const int DEFAULT_GO_DEPTH = 6;

	/// <summary>
	///     Expected number of legal moves from the starting position (20).
	/// </summary>
	public const int EXPECTED_STARTING_POSITION_MOVES = 20;

	/// <summary>
	///     Extended number of iterations for handshake pump loops (50).
	/// </summary>
	public const int EXTENDED_HANDSHAKE_PUMP_ITERATIONS = 50;

	/// <summary>
	///     Number of iterations for handshake pump loops (20-50).
	/// </summary>
	public const int HANDSHAKE_PUMP_ITERATIONS = 20;

	/// <summary>
	///     Small channel capacity for backpressure tests (1).
	/// </summary>
	public const int SMALL_CHANNEL_CAPACITY = 1;

	/// <summary>
	///     FEN after e2e4 (black to move).
	/// </summary>
	public const string AFTER_E2_E4_FEN = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1";

	/// <summary>
	///     UCI perft command template.
	/// </summary>
	public const string GO_PERFT_COMMAND = "go perft 1";

	/// <summary>
	///     UCI ready command.
	/// </summary>
	public const string IS_READY_COMMAND = "isready";

	/// <summary>
	///     FEN for Italian game position.
	/// </summary>
	public const string ITALIAN_GAME_FEN = "r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 2 3";

	/// <summary>
	///     UCI position display command.
	/// </summary>
	public const string POSITION_DISPLAY_COMMAND = "d";

	/// <summary>
	///     FEN for stalemate position.
	/// </summary>
	public const string STALEMATE_FEN = "k7/1QK5/8/8/8/8/8/8 w - - 0 1";

	/// <summary>
	///     Standard starting position FEN.
	/// </summary>
	public const string STANDARD_FEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

	/// <summary>
	///     UCI initialization command.
	/// </summary>
	public const string UCI_COMMAND = "uci";

	/// <summary>
	///     FEN for white mate in one position.
	/// </summary>
	public const string WHITE_MATE_IN_ONE_FEN = "7k/5Q2/7K/8/8/8/8/8 w - - 0 1";

	/// <summary>
	///     Common black responses to e2e4.
	/// </summary>
	public static readonly string[] CommonBlackResponses = ["e7e5", "c7c5"];

	/// <summary>
	///     Common opening moves from starting position.
	/// </summary>
	public static readonly string[] CommonOpeningMoves = ["e2e4", "d2d4", "g1f3", "c2c4"];

	/// <summary>
	///     Delay for backpressure tests (500 milliseconds).
	/// </summary>
	public static readonly TimeSpan BackpressureDelay = TimeSpan.FromMilliseconds(500);

	/// <summary>
	///     Very short timeout for cancellation tests (50 milliseconds).
	/// </summary>
	public static readonly TimeSpan CancellationTimeout = TimeSpan.FromMilliseconds(50);
	/// <summary>
	///     Default timeout for test operations (5 seconds).
	/// </summary>
	public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

	/// <summary>
	///     Extended timeout for slow operations (8 seconds).
	/// </summary>
	public static readonly TimeSpan ExtendedTimeout = TimeSpan.FromSeconds(8);

	/// <summary>
	///     Longer delay for async operations (200 milliseconds).
	/// </summary>
	public static readonly TimeSpan LongerDelay = TimeSpan.FromMilliseconds(200);

	/// <summary>
	///     Long timeout for complex operations (4 seconds).
	/// </summary>
	public static readonly TimeSpan LongTimeout = TimeSpan.FromMilliseconds(4000);

	/// <summary>
	///     Medium delay for async operations (50 milliseconds).
	/// </summary>
	public static readonly TimeSpan MediumDelay = TimeSpan.FromMilliseconds(50);

	/// <summary>
	///     Medium timeout for moderate operations (3 seconds).
	/// </summary>
	public static readonly TimeSpan MediumTimeout = TimeSpan.FromMilliseconds(3000);

	/// <summary>
	///     Quit grace period for process shutdown (100 milliseconds).
	/// </summary>
	public static readonly TimeSpan QuitGracePeriod = TimeSpan.FromMilliseconds(100);

	/// <summary>
	///     Short delay for async operations (10 milliseconds).
	/// </summary>
	public static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(10);

	/// <summary>
	///     Short timeout for quick operations (1.5 seconds).
	/// </summary>
	public static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(1500);

	/// <summary>
	///     Small timeout for quick writes (100 milliseconds).
	/// </summary>
	public static readonly TimeSpan SmallTimeout = TimeSpan.FromMilliseconds(100);

	/// <summary>
	///     Standard delay for async operations (100 milliseconds).
	/// </summary>
	public static readonly TimeSpan StandardDelay = TimeSpan.FromMilliseconds(100);

	/// <summary>
	///     Tiny timeout for write operations (10 milliseconds).
	/// </summary>
	public static readonly TimeSpan TinyTimeout = TimeSpan.FromMilliseconds(10);

	/// <summary>
	///     Very short delay for async operations (5 milliseconds).
	/// </summary>
	public static readonly TimeSpan VeryShortDelay = TimeSpan.FromMilliseconds(5);
}
