using System.Text.RegularExpressions;

namespace Bezoro.UCI.Domain.Constants
{
	internal static class UciConstants
	{
		/// <summary>
		///     Response prefix for the best move command
		/// </summary>
		public const string BEST_MOVE_RESPONSE_PREFIX = "bestmove";

		/// <summary>
		///     Parameter for black's time increment in milliseconds
		/// </summary>
		public const string BLACK_TIME_INCREMENT_PARAMETER = "binc";

		/// <summary>
		///     Parameter for black's remaining time in milliseconds
		/// </summary>
		public const string BLACK_TIME_PARAMETER = "btime";

		/// <summary>
		///     Parameter to specify search depth in plies
		/// </summary>
		public const string DEPTH_PARAMETER = "depth";

		/// <summary>
		///     Command to display current board position
		/// </summary>
		public const string DISPLAY_BOARD_COMMAND = "d";

		/// <summary>
		///     Option to set engine's Elo rating
		/// </summary>
		public const string ELO_OPTION = "UCI_Elo";

		public const string FEN_RESPONSE_PREFIX = "Fen: ";

		/// <summary>
		///     Command to start calculating moves
		/// </summary>
		public const string GO_COMMAND = "go";

		/// <summary>
		///     Command to go to a specific move number in a movie
		/// </summary>
		public const string GO_MOVE_NUMBER_COMMAND = "go movenumber";

		/// <summary>
		///     Command to perform perft test to depth 1
		/// </summary>
		public const string GO_PERFT_DEPTH1_COMMAND = "go perft 1";

		/// <summary>
		///     Parameter for infinite search
		/// </summary>
		public const string INFINITE_SEARCH_PARAMETER = "infinite";

		/// <summary>
		///     Prefix for engine's analysis information
		/// </summary>
		public const string INFO_COMMAND = "info";

		/// <summary>
		///     Command to check if engine is ready
		/// </summary>
		public const string IS_READY_COMMAND = "isready";

		/// <summary>
		///     Option to limit engine strength
		/// </summary>
		public const string LIMIT_STRENGTH_OPTION = "UCI_LimitStrength";

		/// <summary>
		///     Parameter to search for mate in N moves
		/// </summary>
		public const string MATE_SEARCH_PARAMETER = "mate";

		/// <summary>
		///     Parameter for remaining moves until next time control
		/// </summary>
		public const string MOVES_TO_GO_PARAMETER = "movestogo";

		/// <summary>
		///     Parameter for exact move time in milliseconds
		/// </summary>
		public const string MOVE_TIME_PARAMETER = "movetime";

		/// <summary>
		///     Parameter to search exact number of nodes
		/// </summary>
		public const string NODES_SEARCH_PARAMETER = "nodes";

		/// <summary>
		///     Parameter for ponder mode
		/// </summary>
		public const string PONDER_PARAMETER = "ponder";

		/// <summary>
		///     Command to set the current position
		/// </summary>
		public const string POSITION_COMMAND = "position";

		/// <summary>
		///     Command to quit the engine
		/// </summary>
		public const string QUIT_COMMAND = "quit";

		/// <summary>
		///     Response indicating engine is ready
		/// </summary>
		public const string READY_OK_RESPONSE = "readyok";

		/// <summary>
		///     Parameter to restrict search to specific moves
		/// </summary>
		public const string SEARCH_MOVES_PARAMETER = "searchmoves";

		/// <summary>
		///     Command to set an engine option
		/// </summary>
		public const string SET_OPTION_COMMAND = "setoption name";

		public const string STANDARD_FEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

		/// <summary>
		///     Command to set initial chess position
		/// </summary>
		public const string START_POS_COMMAND = "startpos";

		/// <summary>
		///     Command to stop calculation
		/// </summary>
		public const string STOP_COMMAND = "stop";

		/// <summary>
		///     Command to tell engine to use UCI mode
		/// </summary>
		public const string UCI_COMMAND = "uci";

		/// <summary>
		///     Command to start new game
		/// </summary>
		public const string UCI_NEW_GAME_COMMAND = "ucinewgame";

		/// <summary>
		///     Response confirming UCI mode
		/// </summary>
		public const string UCI_OK_RESPONSE = "uciok";

		/// <summary>
		///     Parameter for white's time increment in milliseconds
		/// </summary>
		public const string WHITE_TIME_INCREMENT_PARAMETER = "winc";

		/// <summary>
		///     Parameter for white's remaining time in milliseconds
		/// </summary>
		public const string WHITE_TIME_PARAMETER = "wtime";

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
		public static readonly Regex UciMoveRegex =
			new(@"^[a-h][1-8][a-h][1-8]([qrbn])?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		public const string CHECKERS_RESPONSE = "checkers";
	}
}
