using System;
using System.Linq;

namespace Bezoro.UCI
{
    /// <summary>
    ///     Provides static helper methods for parsing UCI 'info' strings.
    /// </summary>
    internal static class InfoParser
	{
		public static EngineAnalysisEventArgs Parse(string infoLine)
		{
			var args = new EngineAnalysisEventArgs { RawInfo = infoLine };
			if (!infoLine.StartsWith("info "))
			{
				return args;
			}

			string[] tokens = infoLine.Substring(5).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

			for (var i = 0 ; i < tokens.Length - 1 ; i++)
			{
				switch (tokens[i])
				{
					case "depth":
						if (int.TryParse(tokens[i + 1], out int depth))
						{
							args.Depth = depth;
						}

						break;
					case "score":
						if (i + 2 < tokens.Length)
						{
							if (tokens[i + 1] == "cp" && int.TryParse(tokens[i + 2], out int cp))
							{
								args.ScoreCp = cp;
							}
							else if (tokens[i + 1] == "mate" && int.TryParse(tokens[i + 2], out int mate))
							{
								args.Mate = mate;
							}
						}

						break;
					case "nodes":
						if (long.TryParse(tokens[i + 1], out long nodes))
						{
							args.Nodes = nodes;
						}

						break;
					case "nps":
						if (long.TryParse(tokens[i + 1], out long nps))
						{
							args.Nps = nps;
						}

						break;
					case "pv":
						args.PrincipalVariation = tokens.Skip(i + 1).ToList().AsReadOnly();
						return args; // PV is always the last part of the info string.
				}
			}

			return args;
		}
	}
}
