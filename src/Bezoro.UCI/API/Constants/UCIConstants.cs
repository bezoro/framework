using System.Text.RegularExpressions;

namespace Bezoro.UCI.API.Constants
{
	internal static class UCIConstants
	{
		public const string BestMoveResponsePrefix      = "bestmove";
		public const string BlackTimeIncrementParameter = "binc";
		public const string BlackTimeParameter          = "btime";
		public const string DepthParameter              = "depth";
		public const string DisplayBoardCommand         = "d";
		public const string EloOption                   = "UCI_Elo";
		public const string GoCommand                   = "go";
		public const string GoPerftDepth1Command        = "go perft 1";
		public const string InfiniteSearchParameter     = "infinite";
		public const string IsReadyCommand              = "isready";
		public const string LimitStrengthOption         = "UCI_LimitStrength";
		public const string MateSearchParameter         = "mate";
		public const string MoveTimeParameter           = "movetime";
		public const string NodesSearchParameter        = "nodes";
		public const string PositionCommand             = "position";
		public const string QuitCommand                 = "quit";
		public const string ReadyOkResponse             = "readyok";
		public const string SearchMovesParameter        = "searchmoves";
		public const string SetOptionCommand            = "setoption name";
		public const string StartPosCommand             = "startpos";
		public const string StopCommand                 = "stop";
		public const string UCICommand                  = "uci";
		public const string UCINewGameCommand           = "ucinewgame";
		public const string UCIOkResponse               = "uciok";
		public const string WhiteTimeIncrementParameter = "winc";
		public const string WhiteTimeParameter          = "wtime";

		public static readonly Regex
			MoveRegex = new(@"^([a-h][1-8][a-h][1-8][qrbn]?)\s*:\s*\d+", RegexOptions.Compiled);
	}
}
