using System;
using System.Collections.Generic;
using System.Linq;

namespace Bezoro.UCI
{
	/// <summary>
	///     Provides static helper methods for parsing UCI 'info' strings.
	/// </summary>
	internal static class InfoParser
	{
		private const string TokenDepth    = "depth";
		private const string TokenScore    = "score";
		private const string TokenNodes    = "nodes";
		private const string TokenNps      = "nps";
		private const string TokenPv       = "pv";
		private const string ScoreTypeCp   = "cp";
		private const string ScoreTypeMate = "mate";
		private const string InfoPrefix    = "info ";

		private static readonly Dictionary<string, Action<Queue<string>, EngineAnalysisEventArgs>> TokenHandlers = new()
		{
			[TokenDepth] = static (tokens, args) =>
			{
				if (tokens.TryDequeue(out string value) && int.TryParse(value, out int depth))
				{
					args.Depth = depth;
				}
			},
			[TokenScore] = static (tokens, args) =>
			{
				if (!tokens.TryDequeue(out string type) || !tokens.TryDequeue(out string value))
				{
					return;
				}

				switch (type)
				{
					case ScoreTypeCp when int.TryParse(value, out int cp):
						args.ScoreCp = cp;
						break;
					case ScoreTypeMate when int.TryParse(value, out int mate):
						args.Mate = mate;
						break;
				}
			},
			[TokenNodes] = static (tokens, args) =>
			{
				if (tokens.TryDequeue(out string value) && long.TryParse(value, out long nodes))
				{
					args.Nodes = nodes;
				}
			},
			[TokenNps] = static (tokens, args) =>
			{
				if (tokens.TryDequeue(out string value) && long.TryParse(value, out long nps))
				{
					args.Nps = nps;
				}
			}
		};

		public static EngineAnalysisEventArgs Parse(string infoLine)
		{
			var args = new EngineAnalysisEventArgs(infoLine);
			if (!infoLine.StartsWith(InfoPrefix))
			{
				return args;
			}

			var tokens = new Queue<string>(infoLine[5..].Split(' ', StringSplitOptions.RemoveEmptyEntries));

			while (tokens.Count > 0)
			{
				string token = tokens.Dequeue();

				if (token == TokenPv)
				{
					args.PrincipalVariation = tokens.ToList().AsReadOnly();
					return args; // PV is always the last part of the info string.
				}

				if (TokenHandlers.TryGetValue(token, out Action<Queue<string>, EngineAnalysisEventArgs>? handler))
				{
					handler(tokens, args);
				}
			}

			return args;
		}
	}
}
