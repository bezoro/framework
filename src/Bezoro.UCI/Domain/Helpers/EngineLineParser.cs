using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Bezoro.UCI.API.Enums;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI.Domain.Helpers
{
	/// <summary>
	///     Provides static methods for parsing UCI output lines into structured data.
	/// </summary>
	internal static class UCIParser
	{
		private const int InfoCommandLength = 5;
		private const int MinBestMoveTokens = 2;
		private const int MinIdTokens       = 3;
		private const int MinOptionTokens   = 4;
		private const int ScoreTokenAdvance = 2;

		/// <summary>
		///     Parses any UCI output line into a structured EngineOutput.
		/// </summary>
		public static EngineOutput ParseLine(string line)
		{
			if (string.IsNullOrEmpty(line))
			{
				return new EngineOutput(line) { Type = EngineOutputType.Unknown };
			}

			var outputType = GetOutputType(line);
			return outputType switch
			{
				UCIOutputType.Info     => ParseInfoLine(line),
				UCIOutputType.BestMove => ParseBestMoveLine(line),
				UCIOutputType.Option   => ParseOptionLine(line),
				UCIOutputType.Id       => ParseIdLine(line),
				UCIOutputType.Checkers => ParseCheckersLine(line),
				UCIOutputType.UciOk or UCIOutputType.ReadyOk => new EngineOutput(line)
				{
					Type   = EngineOutputType.Status,
					Status = line.Trim()
				},
				_ => new EngineOutput(line) { Type = EngineOutputType.Unknown }
			};
		}

		/// <summary>
		///     Determines the type of UCI output line.
		/// </summary>
		public static UCIOutputType GetOutputType(string line)
		{
			if (string.IsNullOrEmpty(line))
			{
				return UCIOutputType.Unknown;
			}

			return line.Split(' ')[0].ToLowerInvariant() switch
			{
				"info"           => UCIOutputType.Info,
				"bestmove"       => UCIOutputType.BestMove,
				"option"         => UCIOutputType.Option,
				"id"             => UCIOutputType.Id,
				"uciok"          => UCIOutputType.UciOk,
				"readyok"        => UCIOutputType.ReadyOk,
				"copyprotection" => UCIOutputType.CopyProtection,
				"registration"   => UCIOutputType.Registration,
				_                => UCIOutputType.Unknown
			};
		}

		private static (int? ScoreCp, int? Mate) ParseScore(string scoreType, string scoreValue)
		{
			return scoreType.ToLowerInvariant() switch
			{
				"cp" when int.TryParse(scoreValue,   out int cp)   => (cp, null),
				"mate" when int.TryParse(scoreValue, out int mate) => (null, mate),
				_                                                  => (null, null)
			};
		}

		private static bool TryGetNext(string[] tokens, ref int index, out string value)
		{
			if (index + 1 < tokens.Length)
			{
				value = tokens[++index];
				return true;
			}

			value = string.Empty;
			return false;
		}

		private static bool TryGetNextInt(string[] tokens, ref int index, out int value)
		{
			if (index + 1 < tokens.Length && int.TryParse(tokens[index + 1], out value))
			{
				index++;
				return true;
			}

			value = 0;
			return false;
		}

		private static bool TryGetNextLong(string[] tokens, ref int index, out long value)
		{
			if (index + 1 < tokens.Length && long.TryParse(tokens[index + 1], out value))
			{
				index++;
				return true;
			}

			value = 0;
			return false;
		}

		private static EngineAnalysisInfo ProcessInfoToken(string[] tokens, ref int index, EngineAnalysisInfo info)
		{
			return tokens[index] switch
			{
				"depth" when TryGetNextInt(tokens,    ref index, out int depth)    => info with { Depth = depth },
				"seldepth" when TryGetNextInt(tokens, ref index, out int selDepth) => info with { SelDepth = selDepth },
				"nodes" when TryGetNextLong(tokens, ref index, out long nodes)     => info with { Nodes = nodes },
				"nps" when TryGetNextLong(tokens,   ref index, out long nps)       => info with { Nps = nps },
				"time" when TryGetNextLong(tokens,  ref index, out long time)      => info with { TimeMs = time },
				"hashfull" when TryGetNextInt(tokens, ref index, out int hashFull) => info with { HashFull = hashFull },
				"tbhits" when TryGetNextLong(tokens, ref index, out long tbHits)   => info with { TbHits = tbHits },
				"currmove" when TryGetNext(tokens, ref index, out string currMove) => info with
				{
					CurrentMove = currMove
				},
				"currmovenumber" when TryGetNextInt(tokens, ref index, out int currMoveNum) => info with
				{
					CurrentMoveNumber = currMoveNum
				},
				"score" when index + 2 < tokens.Length => ProcessScoreToken(tokens, ref index, info),
				"pv"                                   => ProcessPrincipalVariation(tokens, index, info),
				_                                      => info
			};
		}

		private static EngineAnalysisInfo ProcessPrincipalVariation(string[] tokens, int index, EngineAnalysisInfo info)
		{
			ReadOnlyCollection<string> principalVariation = tokens.Skip(index + 1).ToList().AsReadOnly();
			return info with { PrincipalVariation = principalVariation };
		}

		private static EngineAnalysisInfo ProcessScoreToken(string[] tokens, ref int index, EngineAnalysisInfo info)
		{
			(int? scoreCp, int? mate) =  ParseScore(tokens[index + 1], tokens[index + 2]);
			index                     += ScoreTokenAdvance;
			return info with { ScoreCp = scoreCp, Mate = mate };
		}

		private static EngineOutput ParseBestMoveLine(string line)
		{
			string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < MinBestMoveTokens)
			{
				return new EngineOutput(line) { Type = EngineOutputType.Unknown };
			}

			string  bestMove   = parts[1];
			string? ponderMove = ExtractPonderMove(parts);
			return new EngineOutput(line)
			{
				Type       = EngineOutputType.BestMove,
				BestMove   = bestMove,
				PonderMove = ponderMove
			};
		}

		private static EngineOutput ParseCheckersLine(string line)
		{
			if (!line.Contains("Checkers:"))
			{
				return new EngineOutput(line) { Type = EngineOutputType.Unknown };
			}

			string checkers = line.Split("Checkers:").Last().Trim();
			return new EngineOutput(line) { Checkers = checkers };
		}

		private static EngineOutput ParseIdLine(string line)
		{
			string[] parts = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < MinIdTokens)
			{
				return new EngineOutput(line) { Type = EngineOutputType.Unknown };
			}

			var id = new EngineId();
			switch (parts[1].ToLowerInvariant())
			{
				case "name":
					id = id with { Name = parts[2] };
					break;
				case "author":
					id = id with { Author = parts[2] };
					break;
			}

			return new EngineOutput(line)
			{
				Type = EngineOutputType.Id,
				Id   = id
			};
		}

		private static EngineOutput ParseInfoLine(string line)
		{
			string[] tokens = line.AsSpan(InfoCommandLength).ToString().
								   Split(' ', StringSplitOptions.RemoveEmptyEntries);

			if (tokens.Length > 0 && tokens[0].Equals("string", StringComparison.OrdinalIgnoreCase))
			{
				return new EngineOutput(line)
				{
					Type         = EngineOutputType.Info,
					AnalysisInfo = null // No analysis data for string messages
				};
			}

			var info = new EngineAnalysisInfo(line);

			for (var i = 0 ; i < tokens.Length ; i++)
			{
				info = ProcessInfoToken(tokens, ref i, info);
			}

			return new EngineOutput(line)
			{
				Type         = EngineOutputType.Info,
				AnalysisInfo = info
			};
		}

		private static EngineOutput ParseOptionLine(string line)
		{
			string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length < MinOptionTokens || !tokens[1].Equals("name", StringComparison.OrdinalIgnoreCase))
			{
				return new EngineOutput(line) { Type = EngineOutputType.Unknown };
			}

			var     option    = new EngineOption();
			var     variables = new List<string>();
			string? name      = null;
			for (var i = 2 ; i < tokens.Length ; i++)
			{
				switch (tokens[i].ToLowerInvariant())
				{
					case "type" when i + 1 < tokens.Length:
						option = option with { Type = tokens[++i] };
						break;
					case "default" when i + 1 < tokens.Length:
						option = option with { DefaultValue = tokens[++i] };
						break;
					case "min" when i + 1 < tokens.Length:
						option = option with { MinValue = tokens[++i] };
						break;
					case "max" when i + 1 < tokens.Length:
						option = option with { MaxValue = tokens[++i] };
						break;
					case "var" when i + 1 < tokens.Length:
						variables.Add(tokens[++i]);
						break;
					default:
						name = string.IsNullOrEmpty(name) ? tokens[i] : $"{name} {tokens[i]}";
						break;
				}
			}

			option = option with
			{
				Name = name,
				Variables = variables.Count > 0 ? variables.AsReadOnly() : null
			};

			return new EngineOutput(line)
			{
				Type   = EngineOutputType.Option,
				Option = option
			};
		}

		private static string? ExtractPonderMove(string[] parts)
		{
			for (var i = 2 ; i < parts.Length - 1 ; i++)
			{
				if (parts[i].Equals("ponder", StringComparison.OrdinalIgnoreCase))
				{
					return parts[i + 1];
				}
			}

			return null;
		}
	}

	/// <summary>
	///     Represents different types of UCI output lines.
	/// </summary>
	internal enum UCIOutputType
	{
		Unknown,
		Info,
		BestMove,
		Option,
		Id,
		UciOk,
		ReadyOk,
		Checkers,
		CopyProtection,
		Registration
	}
}
