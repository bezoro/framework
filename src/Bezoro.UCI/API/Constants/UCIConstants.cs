using System.Text.RegularExpressions;

namespace Bezoro.UCI.API.Constants
{
	internal static class UCIConstants
	{
		/// <summary>
		///     Response prefix for the best move command
		/// </summary>
		public const string BestMoveResponsePrefix = "bestmove";

		/// <summary>
		///     Parameter for black's time increment in milliseconds
		/// </summary>
		public const string BlackTimeIncrementParameter = "binc";

		/// <summary>
		///     Parameter for black's remaining time in milliseconds
		/// </summary>
		public const string BlackTimeParameter = "btime";

		/// <summary>
		///     Parameter to specify search depth in plies
		/// </summary>
		public const string DepthParameter = "depth";

		/// <summary>
		///     Command to display current board position
		/// </summary>
		public const string DisplayBoardCommand = "d";

		/// <summary>
		///     Option to set engine's Elo rating
		/// </summary>
		public const string EloOption = "UCI_Elo";

		/// <summary>
		///     Command to start calculating moves
		/// </summary>
		public const string GoCommand = "go";
		/// <summary>
		///     Command to go to a specific move number in a movie
		/// </summary>
		public const string GoMoveNumberCommand = "go movenumber";

		/// <summary>
		///     Command to perform perft test to depth 1
		/// </summary>
		public const string GoPerftDepth1Command = "go perft 1";

		/// <summary>
		///     Parameter for infinite search
		/// </summary>
		public const string InfiniteSearchParameter = "infinite";

		/// <summary>
		///     Prefix for engine's analysis information
		/// </summary>
		public const string InfoCommand = "info";

		/// <summary>
		///     Command to check if engine is ready
		/// </summary>
		public const string IsReadyCommand = "isready";

		/// <summary>
		///     Option to limit engine strength
		/// </summary>
		public const string LimitStrengthOption = "UCI_LimitStrength";

		/// <summary>
		///     Parameter to search for mate in N moves
		/// </summary>
		public const string MateSearchParameter = "mate";

		/// <summary>
		///     Parameter for exact move time in milliseconds
		/// </summary>
		public const string MoveTimeParameter = "movetime";

		/// <summary>
		///     Parameter to search exact number of nodes
		/// </summary>
		public const string NodesSearchParameter = "nodes";

		/// <summary>
		///     Command to set the current position
		/// </summary>
		public const string PositionCommand = "position";

		/// <summary>
		///     Command to quit the engine
		/// </summary>
		public const string QuitCommand = "quit";

		/// <summary>
		///     Response indicating engine is ready
		/// </summary>
		public const string ReadyOkResponse = "readyok";

		/// <summary>
		///     Parameter to restrict search to specific moves
		/// </summary>
		public const string SearchMovesParameter = "searchmoves";

		/// <summary>
		///     Command to set an engine option
		/// </summary>
		public const string SetOptionCommand = "setoption name";

		/// <summary>
		///     Command to set initial chess position
		/// </summary>
		public const string StartPosCommand = "startpos";

		/// <summary>
		///     Command to stop calculation
		/// </summary>
		public const string StopCommand = "stop";

		/// <summary>
		///     Command to tell engine to use UCI mode
		/// </summary>
		public const string UCICommand = "uci";

		/// <summary>
		///     Command to start new game
		/// </summary>
		public const string UCINewGameCommand = "ucinewgame";

		/// <summary>
		///     Response confirming UCI mode
		/// </summary>
		public const string UCIOkResponse = "uciok";

		/// <summary>
		///     Parameter for white's time increment in milliseconds
		/// </summary>
		public const string WhiteTimeIncrementParameter = "winc";

		/// <summary>
		///     Parameter for white's remaining time in milliseconds
		/// </summary>
		public const string WhiteTimeParameter = "wtime";

		/// <summary>
		///     Regular expression for validating chess square notation (e.g., "e4", "a1").
		/// </summary>
		public static readonly Regex AlgebraicNotationRegex
			= new("^[a-h][1-8]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>
		///     Regular expression for validating Forsyth–Edwards Notation (FEN) strings.
		/// </summary>
		public static readonly Regex FenRegex =
			new(@"^([rnbqkpRNBQKP1-8]+\/){7}[rnbqkpRNBQKP1-8]+\s[bw]\s(-|K?Q?k?q?)\s(-|[a-h][36])\s\d+\s\d+$",
				RegexOptions.Compiled);

		/// <summary>
		///     Regular expression for validating move strings with their scores.
		/// </summary>
		public static readonly Regex MoveRegex =
			new(@"^([a-h][1-8][a-h][1-8][qrbn]?)\s*:\s*\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>
		///     Regular expression for validating UCI move notation.
		/// </summary>
		public static readonly Regex UCIMoveRegex =
			new(@"^[a-h][1-8][a-h][1-8]([qrbn])?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
	}
}
