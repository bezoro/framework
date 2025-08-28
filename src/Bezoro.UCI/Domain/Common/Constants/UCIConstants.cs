using System.Text.RegularExpressions;

namespace Bezoro.UCI.Domain.Common.Constants;

internal static class UciConstants
{
	/// <summary>
	///     UCI commands
	/// </summary>
	internal static class Commands
	{
		/// <summary>
		///     Command to enable/disable debug mode
		/// </summary>
		public const string DEBUG = "debug";
		/// <summary>
		///     Command to display current board position
		/// </summary>
		public const string DISPLAY_BOARD = "d";
		/// <summary>
		///     Command to start calculating moves
		/// </summary>
		public const string GO = "go";
		/// <summary>
		///     Command to go to a specific move number in a game
		/// </summary>
		public const string GO_MOVE_NUMBER = "go movenumber";
		/// <summary>
		///     Command to perform perft test (depth specified separately)
		/// </summary>
		public const string GO_PERFT = "go perft";
		/// <summary>
		///     Command to check if engine is ready
		/// </summary>
		public const string IS_READY = "isready";
		/// <summary>
		///     Command to notify engine that ponder move was played
		/// </summary>
		public const string PONDER_HIT = "ponderhit";
		/// <summary>
		///     Command to set the current position
		/// </summary>
		public const string POSITION = "position";
		/// <summary>
		///     Command to quit the engine
		/// </summary>
		public const string QUIT = "quit";
		/// <summary>
		///     Command to register the engine (license/activation)
		/// </summary>
		public const string REGISTER = "register";
		/// <summary>
		///     Command to set an engine option
		/// </summary>
		public const string SET_OPTION = "setoption name";
		/// <summary>
		///     Command to set initial chess position
		/// </summary>
		public const string START_POS = "startpos";
		/// <summary>
		///     Command to stop calculation
		/// </summary>
		public const string STOP = "stop";
		/// <summary>
		///     Command to tell engine to use UCI mode
		/// </summary>
		public const string UCI = "uci";
		/// <summary>
		///     Command to start new game
		/// </summary>
		public const string UCI_NEW_GAME = "ucinewgame";
	}

	/// <summary>
	///     Useful FEN positions.
	/// </summary>
	internal static class Fen
	{
		/// <summary>
		///     Position that is already checkmate for the side to move.
		///     Black: Ka8; White: Kb6, Qb7; Black to move and checkmated.
		/// </summary>
		public const string BLACK_ALREADY_MATE = "k7/1Q6/1K6/8/8/8/8/8 b - - 0 1";
		/// <summary>
		///     Position that is already stalemate for the side to move.
		///     Black: Kh8; White: Kg6, Qf7; Black to move has no legal moves and is not in check.
		/// </summary>
		public const string BLACK_ALREADY_STALEMATE = "7k/5Q2/6K1/8/8/8/8/8 b - - 0 1";
		/// <summary>
		///     Position where the side to move is in check (but not checkmated).
		///     Black: Ke8; White: Re1; Black to move and in check.
		/// </summary>
		public const string BLACK_CHECK = "4k3/8/8/8/8/8/8/4R3 b - - 0 1";
		/// <summary>
		///     Position where side to move (Black) can deliver mate in one.
		///     Black: Kh3, Qg3; White: Kh1; Black to move plays Qg2#.
		/// </summary>
		public const string BLACK_MATE_IN_ONE = "8/8/8/8/8/6qk/8/7K b - - 0 1";
		/// <summary>
		///     Position where side to move (Black) can force stalemate in one.
		///     Black: Kg3, Qf3; White: Kh1; Black to move plays Qf2 to stalemate.
		/// </summary>
		public const string BLACK_STALEMATE_IN_ONE = "8/8/8/8/8/5qk1/8/7K b - - 0 1";
		/// <summary>
		///     Standard starting position in FEN notation
		/// </summary>
		public const string STANDARD = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
		/// <summary>
		///     Position that is already checkmate for the side to move (White).
		///     White: Ka1; Black: Kb3, Qb2; White to move and checkmated.
		/// </summary>
		public const string WHITE_ALREADY_MATE = "8/8/8/8/8/1k6/1q6/K7 w - - 0 1";
		/// <summary>
		///     Position that is already stalemate for the side to move (White).
		///     White: Kh1; Black: Kg3, Qf2; White to move has no legal moves and is not in check.
		/// </summary>
		public const string WHITE_ALREADY_STALEMATE = "8/8/8/8/8/5k1/5q2/7K w - - 0 1";
		/// <summary>
		///     Position where the side to move is in check (but not checkmated) — White to move.
		///     White: Ke1; Black: Re8; White to move and in check.
		/// </summary>
		public const string WHITE_CHECK = "4r3/8/8/8/8/8/8/4K3 w - - 0 1";
		/// <summary>
		///     Position where side to move can deliver mate in one.
		///     White: Kh6, Qg6; Black: Kh8; White to move plays Qg7#.
		/// </summary>
		public const string WHITE_MATE_IN_ONE = "7k/8/6QK/8/8/8/8/8 w - - 0 1";
		/// <summary>
		///     Position where side to move can force stalemate in one.
		///     White: Kg6, Qf6; Black: Kh8; White to move plays Qf7 to stalemate.
		/// </summary>
		public const string WHITE_STALEMATE_IN_ONE = "7k/8/5QK1/8/8/8/8/8 w - - 0 1";
	}

	/// <summary>
	///     Keywords used in engine responses and commands
	/// </summary>
	internal static class Keywords
	{
		/// <summary>
		///     Keyword used in 'id' response for the author's name
		/// </summary>
		public const string AUTHOR = "author";
		/// <summary>
		///     Keyword used in 'register' command to pass registration code
		/// </summary>
		public const string CODE = "code";
		/// <summary>
		///     Keyword used with the 'position' command to specify FEN input
		/// </summary>
		public const string FEN = "fen";
		/// <summary>
		///     Keyword used in 'register' command to defer registration
		/// </summary>
		public const string LATER = "later";
		/// <summary>
		///     Keyword used with the 'position' command to specify subsequent moves
		/// </summary>
		public const string MOVES = "moves";
		/// <summary>
		///     Keyword used in 'setoption' and 'id' responses
		/// </summary>
		public const string NAME = "name";
		/// <summary>
		///     Keyword used in 'debug' command to disable debugging
		/// </summary>
		public const string OFF = "off";
		/// <summary>
		///     Keyword used in 'debug' command to enable debugging
		/// </summary>
		public const string ON = "on";
		/// <summary>
		///     Keyword used in 'setoption' to specify option value
		/// </summary>
		public const string VALUE = "value";
	}

	/// <summary>
	///     Engine options
	/// </summary>
	internal static class Options
	{
		/// <summary>
		///     Option to run engine in analysis mode
		/// </summary>
		public const string ANALYSE_MODE = "UCI_AnalyseMode";
		/// <summary>
		///     Option to enable Chess960 (Fischer Random Chess) support
		/// </summary>
		public const string CHESS960 = "UCI_Chess960";
		/// <summary>
		///     Option to set engine's Elo rating
		/// </summary>
		public const string ELO = "UCI_Elo";
		/// <summary>
		///     Option to limit engine strength
		/// </summary>
		public const string LIMIT_STRENGTH = "UCI_LimitStrength";
	}

	/// <summary>
	///     Engine parameters
	/// </summary>
	internal static class Parameters
	{
		/// <summary>
		///     Parameter for black's remaining time in milliseconds
		/// </summary>
		public const string BLACK_TIME = "btime";
		/// <summary>
		///     Parameter for black's time increment in milliseconds
		/// </summary>
		public const string BLACK_TIME_INCREMENT = "binc";
		/// <summary>
		///     Parameter to specify search depth in plies
		/// </summary>
		public const string DEPTH = "depth";
		/// <summary>
		///     Parameter for infinite search
		/// </summary>
		public const string INFINITE = "infinite";
		/// <summary>
		///     Parameter to search for mate in N moves
		/// </summary>
		public const string MATE = "mate";
		/// <summary>
		///     Parameter for exact move time in milliseconds
		/// </summary>
		public const string MOVE_TIME = "movetime";
		/// <summary>
		///     Parameter for remaining moves until next time control
		/// </summary>
		public const string MOVES_TO_GO = "movestogo";
		/// <summary>
		///     Parameter to search exact number of nodes
		/// </summary>
		public const string NODES = "nodes";
		/// <summary>
		///     Parameter for ponder mode
		/// </summary>
		public const string PONDER = "ponder";
		/// <summary>
		///     Parameter to restrict search to specific moves
		/// </summary>
		public const string SEARCH_MOVES = "searchmoves";
		/// <summary>
		///     Parameter for white's remaining time in milliseconds
		/// </summary>
		public const string WHITE_TIME = "wtime";
		/// <summary>
		///     Parameter for white's time increment in milliseconds
		/// </summary>
		public const string WHITE_TIME_INCREMENT = "winc";
	}

	internal static class Pieces
	{
		public const char CHAR_BLACK_BISHOP = 'b';
		public const char CHAR_BLACK_KING   = 'k';
		public const char CHAR_BLACK_KNIGHT = 'n';
		public const char CHAR_BLACK_PAWN   = 'p';
		public const char CHAR_BLACK_QUEEN  = 'q';
		public const char CHAR_BLACK_ROOK   = 'r';

		public const char CHAR_WHITE_BISHOP = 'B';
		public const char CHAR_WHITE_KING   = 'K';
		public const char CHAR_WHITE_KNIGHT = 'N';
		public const char CHAR_WHITE_PAWN   = 'P';
		public const char CHAR_WHITE_QUEEN  = 'Q';
		public const char CHAR_WHITE_ROOK   = 'R';

		public const string CHARS_ALL           = "prnbqkPRNBQK";
		public const string CHARS_ALL_BLACK     = "prnbqk";
		public const string CHARS_ALL_PROMOTION = "rnbqRNBQ";
		public const string CHARS_ALL_WHITE     = "PRNBQK";

		public const string CHARS_BISHOP = "bB";
		public const string CHARS_KING   = "kK";
		public const string CHARS_KNIGHT = "nN";
		public const string CHARS_PAWN   = "pP";
		public const string CHARS_QUEEN  = "qQ";
		public const string CHARS_ROOK   = "rR";
	}

	/// <summary>
	///     Prefixes for engine responses
	/// </summary>
	internal static class Prefixes
	{
		/// <summary>
		///     Response prefix for the best move command
		/// </summary>
		public const string BEST_MOVE = "bestmove";
		/// <summary>
		///     Response prefix for checkers from 'd' command
		/// </summary>
		public const string CHECKERS = "checkers";
		/// <summary>
		///     Response prefix for FEN string output
		/// </summary>
		public const string FEN = "Fen: ";
		/// <summary>
		///     Prefix for engine identification lines
		/// </summary>
		public const string ID = "id";
		/// <summary>
		///     Prefix for engine's analysis information
		/// </summary>
		public const string INFO = "info";
		/// <summary>
		///     Prefix for engine option definition lines
		/// </summary>
		public const string OPTION = "option";
	}

	internal static class Regex
	{
		/// <summary>
		///     Timeout duration for regex pattern matching operations
		/// </summary>
		private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);
		/// <summary>
		///     Regular expression for validating chess square notation (e.g., "e4", "a1").
		/// </summary>
		public static readonly System.Text.RegularExpressions.Regex AlgebraicNotationRegex
			= new(
				"^[a-h][1-8]$",
				RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
				RegexTimeout);
		/// <summary>
		///     Regular expression for validating Forsyth–Edwards Notation (FEN) strings.
		///     Note: This checks only basic syntax, not semantic validity (e.g., rank sums, king counts).
		/// </summary>
		public static readonly System.Text.RegularExpressions.Regex FenRegex =
			new(
				@"^([rnbqkpRNBQKP1-8]+\/){7}[rnbqkpRNBQKP1-8]+\s[bw]\s(-|(?=[KQkq]+)K?Q?k?q?)\s(-|[a-h][36])\s\d+\s\d+$",
				RegexOptions.Compiled | RegexOptions.CultureInvariant,
				RegexTimeout);
		/// <summary>
		///     Regular expression for validating UCI move notation.
		/// </summary>
		public static readonly System.Text.RegularExpressions.Regex UciMoveRegex =
			new(
				@"^[a-h][1-8][a-h][1-8]([qrbn])?$",
				RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
				RegexTimeout);
	}

	internal static class Responses
	{
		/// <summary>
		///     Response indicating engine is ready
		/// </summary>
		public const string RESPONSE_READY_OK = "readyok";
		/// <summary>
		///     Response confirming UCI mode
		/// </summary>
		public const string RESPONSE_UCI_OK = "uciok";
	}
}
