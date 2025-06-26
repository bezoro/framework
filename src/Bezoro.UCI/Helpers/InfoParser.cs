using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.UCI.Types;

namespace Bezoro.UCI.Helpers
{
	/// <summary>
	///     Provides static helper methods for parsing UCI 'info' strings.
	/// </summary>
	internal static class InfoParser
	{
		private const string InfoPrefix    = "info ";
		private const string ScoreTypeCp   = "cp";
		private const string ScoreTypeMate = "mate";
		private const string TokenDepth    = "depth";
		private const string TokenNodes    = "nodes";
		private const string TokenNps      = "nps";
		private const string TokenPv       = "pv";
		private const string TokenScore    = "score";
		private static readonly Dictionary<string, Action<Queue<string>, EngineAnalysisEventArgs>> TokenHandlers = new()
		{
			[TokenDepth] = static (tokens, args) => ParseIntValue(tokens, value => args.Depth = value),
			[TokenScore] = ParseScore,
			[TokenNodes] = static (tokens, args) => ParseLongValue(tokens, value => args.Nodes = value),
			[TokenNps]   = static (tokens, args) => ParseLongValue(tokens, value => args.Nps = value),
			[TokenPv] = static (tokens, args) =>
			{
				args.PrincipalVariation = tokens.ToList().AsReadOnly();
				tokens.Clear(); // PV is always the last part of the info string.
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
			while (tokens.TryDequeue(out string? token))
			{
				if (TokenHandlers.TryGetValue(token, out Action<Queue<string>, EngineAnalysisEventArgs>? handler))
				{
					handler(tokens, args);
				}
			}

			return args;
		}

		private static void ParseIntValue(Queue<string> tokens, Action<int> setter)
		{
			if (tokens.TryDequeue(out string value) && int.TryParse(value, out int result))
			{
				setter(result);
			}
		}

		private static void ParseLongValue(Queue<string> tokens, Action<long> setter)
		{
			if (tokens.TryDequeue(out string value) && long.TryParse(value, out long result))
			{
				setter(result);
			}
		}

		private static void ParseScore(Queue<string> tokens, EngineAnalysisEventArgs args)
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
		}
	}
}
