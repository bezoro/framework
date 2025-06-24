using System.Text.RegularExpressions;

namespace Bezoro.UCI.API.Constants
{
	internal static class UCIConstants
	{
		public const string BestMoveResponse  = "bestmove";
		public const string Binc              = "binc";
		public const string Btime             = "btime";
		public const string DCommand          = "d";
		public const string Depth             = "depth";
		public const string GoCommand         = "go";
		public const string GOPerftOne        = "go perft 1";
		public const string Infinite          = "infinite";
		public const string IsReadyCommand    = "isready";
		public const string Mate              = "mate";
		public const string Movetime          = "movetime";
		public const string Nodes             = "nodes";
		public const string PositionCommand   = "position";
		public const string QuitCommand       = "quit";
		public const string ReadyOkResponse   = "readyok";
		public const string SearchMoves       = "searchmoves";
		public const string SetOptionCommand  = "setoption name";
		public const string StartPosCommand   = "startpos";
		public const string StopCommand       = "stop";
		public const string UCICommand        = "uci";
		public const string UCIElo            = "UCI_Elo";
		public const string UCILimitstrength  = "UCI_LimitStrength";
		public const string UCINewGameCommand = "ucinewgame";
		public const string UCIOkResponse     = "uciok";
		public const string Winc              = "winc";
		public const string Wtime             = "wtime";

		public static readonly Regex
			MoveRegex = new(@"^([a-h][1-8][a-h][1-8][qrbn]?)\s*:\s*\d+", RegexOptions.Compiled);
	}
}
