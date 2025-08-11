using System.Collections.Generic;

namespace Bezoro.UCI.API.Types;

public enum UciBound
{
	None  = 0,
	Lower = 1,
	Upper = 2
}

public readonly record struct UciSearchInfo(
	int?     Depth,
	int?     SelDepth,
	int?     MultiPv,
	int?     ScoreCp,
	int?     ScoreMate,
	UciBound ScoreBound,
	long?    Nodes,
	long?    Nps,
	int?     HashFullPermille,
	long?    TbHits,
	int?     TimeMs,
	string?  CurrMove,
	int?     CurrMoveNumber,
	string[] Pv,
	string?  BestMove,
	string?  Ponder,
	string   Raw)
{
	public static bool TryParse(string line, out UciSearchInfo info)
	{
		info = default;

		if (string.IsNullOrWhiteSpace(line)) return false;

		info = ExtractUciSearchInfoData(line);

		if (TryExtractBestMoveAndPonder(line, out var result))
		{
			info = info with
			{
				BestMove = result.bestmove,
				Ponder = result.ponder
			};
		}

		return true;
	}

	private static bool TryExtractBestMoveAndPonder(string line, out (string? bestmove, string? ponder) result)
	{
		result = (null, null);
		if (!line.StartsWith("bestmove", StringComparison.OrdinalIgnoreCase)) return false;

		string[]? tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

		string? best = tokens.Length >= 2 ? tokens[1] : null;
		if (string.Equals(best, "(none)", StringComparison.OrdinalIgnoreCase)) best = "none";

		string? ponder = null;

		for (var i = 2; i + 1 < tokens.Length; i++)
		{
			if (tokens[i] != "ponder") continue;

			ponder = tokens[i + 1];
			break;
		}

		result = (best, ponder);
		return true;
	}

	private static UciSearchInfo ExtractUciSearchInfoData(string line)
	{
		if (!line.StartsWith("info", StringComparison.OrdinalIgnoreCase)) return default;

		string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (tokens.Length == 0) return default;

		int?    depth          = null;
		int?    selDepth       = null;
		int?    multiPv        = null;
		int?    scoreCp        = null;
		int?    scoreMate      = null;
		var     bound          = UciBound.None;
		long?   nodes          = null;
		long?   nps            = null;
		int?    hashfull       = null;
		long?   tbhits         = null;
		int?    timeMs         = null;
		string? currMove       = null;
		int?    currMoveNumber = null;
		var     pvMoves        = new List<string>();

		for (var i = 1; i < tokens.Length; i++)
		{
			string tok = tokens[i];

			switch (tok)
			{
				case "depth":
					if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out int d))
					{
						depth = d;
						i++;
					}

					break;

				case "seldepth":
					if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out int sd))
					{
						selDepth = sd;
						i++;
					}

					break;

				case "multipv":
					if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out int mpv))
					{
						multiPv = mpv;
						i++;
					}

					break;

				case "score":
					// Expected patterns: "score cp X [lowerbound|upperbound]" or "score mate Y [lowerbound|upperbound]"
					if (i + 2 < tokens.Length)
					{
						string type = tokens[i + 1];
						string val  = tokens[i + 2];

						if (type == "cp")
						{
							if (int.TryParse(val, out int cp)) scoreCp = cp;
							i += 2;
						}
						else if (type == "mate")
						{
							if (int.TryParse(val, out int mate)) scoreMate = mate;
							i += 2;
						}

						// Optional bound token may follow
						if (i + 1 < tokens.Length)
						{
							string maybeBound = tokens[i + 1];

							if (maybeBound == "lowerbound")
							{
								bound = UciBound.Lower;
								i++;
							}
							else if (maybeBound == "upperbound")
							{
								bound = UciBound.Upper;
								i++;
							}
						}
					}

					break;

				case "nodes":
					if (i + 1 < tokens.Length && long.TryParse(tokens[i + 1], out long n))
					{
						nodes = n;
						i++;
					}

					break;

				case "nps":
					if (i + 1 < tokens.Length && long.TryParse(tokens[i + 1], out long perSec))
					{
						nps = perSec;
						i++;
					}

					break;

				case "hashfull":
					if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out int hf))
					{
						hashfull = hf;
						i++;
					}

					break;

				case "tbhits":
					if (i + 1 < tokens.Length && long.TryParse(tokens[i + 1], out long tb))
					{
						tbhits = tb;
						i++;
					}

					break;

				case "time":
					if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out int t))
					{
						timeMs = t;
						i++;
					}

					break;

				case "currmove":
					if (i + 1 < tokens.Length)
					{
						currMove = tokens[i + 1];
						i++;
					}

					break;

				case "currmovenumber":
					if (i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out int cmn))
					{
						currMoveNumber = cmn;
						i++;
					}

					break;

				case "pv":
					for (int j = i + 1; j < tokens.Length; j++)
						pvMoves.Add(tokens[j]);

					i = tokens.Length;
					break;
			}
		}

		return new(
			depth,
			selDepth,
			multiPv,
			scoreCp,
			scoreMate,
			bound,
			nodes,
			nps,
			hashfull,
			tbhits,
			timeMs,
			currMove,
			currMoveNumber,
			pvMoves.ToArray(),
			null,
			null,
			line
		);
	}
}
