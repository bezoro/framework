using System.Collections.Generic;
using Bezoro.Chess.UCI.Protocol.Domain.EngineClient;
using Bezoro.Chess.UCI.Protocol.Domain.Common.Constants;

namespace Bezoro.Chess.UCI.Protocol.Domain.Common.Helpers;

/// <summary>
///     Parses raw engine output lines into typed UCI protocol messages.
/// </summary>
internal static class UciProtocolParser
{
	public static bool TryParse(string line, out UciProtocolMessage? message)
	{
		message = null;
		if (string.IsNullOrWhiteSpace(line)) return false;

		string trimmed = line.Trim();

		if (trimmed.Equals(UciConstants.Responses.UCI_OK, StringComparison.OrdinalIgnoreCase))
		{
			message = new UciUciOkMessage(trimmed);
			return true;
		}

		if (trimmed.Equals(UciConstants.Responses.READY_OK, StringComparison.OrdinalIgnoreCase))
		{
			message = new UciReadyOkMessage(trimmed);
			return true;
		}

		if (TryParseId(trimmed, out var idMessage))
		{
			message = idMessage;
			return true;
		}

		if (UciEngineOption.TryParse(trimmed, out var option))
		{
			message = new UciOptionMessage(option, trimmed);
			return true;
		}

		if (BestMoveLine.TryParse(trimmed, out var bestMove))
		{
			message = new UciBestMoveMessage(
				bestMove.BestMove,
				bestMove.PonderMove ?? string.Empty,
				trimmed
			);
			return true;
		}

		if (TryParseProtection(trimmed, "copyprotection", static (state, raw) => new UciCopyProtectionMessage(state, raw), out var copyProtection))
		{
			message = copyProtection;
			return true;
		}

		if (TryParseProtection(trimmed, "registration", static (state, raw) => new UciRegistrationMessage(state, raw), out var registration))
		{
			message = registration;
			return true;
		}

		if (TryParseInfo(trimmed, out var info))
		{
			message = info;
			return true;
		}

		return false;
	}

	private static bool TryParseId(string line, out UciIdMessage? message)
	{
		message = null;

		if (TryParseIdLine(line, UciConstants.Keywords.NAME, UciIdKind.Name, out message))
			return true;

		return TryParseIdLine(line, UciConstants.Keywords.AUTHOR, UciIdKind.Author, out message);
	}

	private static bool TryParseIdLine(
		string         line,
		string         idToken,
		UciIdKind      kind,
		out UciIdMessage? message)
	{
		string prefix = $"{UciConstants.Prefixes.ID} {idToken} ";
		if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			message = new(kind, line[prefix.Length..].Trim(), line);
			return true;
		}

		message = null;
		return false;
	}

	private static bool TryParseInfo(string line, out UciInfoMessage? message)
	{
		message = null;
		if (!line.StartsWith($"{UciConstants.Prefixes.INFO} ", StringComparison.OrdinalIgnoreCase))
			return false;

		string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (tokens.Length < 2) return false;

		uint? depth = null;
		uint? selDepth = null;
		uint? multiPv = null;
		uint? nodes = null;
		uint? nps = null;
		uint? tbHits = null;
		uint? time = null;
		uint? hashFull = null;
		uint? cpuLoad = null;
		string? currentMove = null;
		uint? currentMoveNumber = null;
		uint? currentLineCpu = null;
		string? infoString = null;
		UciInfoScore? score = null;
		List<string>? refutation = null;
		List<string>? currentLine = null;
		PrincipalVariation? principalVariation = null;

		for (var i = 1; i < tokens.Length; i++)
		{
			switch (tokens[i])
			{
				case "depth" when TryReadUInt(tokens, i + 1, out uint depthValue):
					depth = depthValue;
					i++;
					break;
				case "seldepth" when TryReadUInt(tokens, i + 1, out uint selDepthValue):
					selDepth = selDepthValue;
					i++;
					break;
				case "multipv" when TryReadUInt(tokens, i + 1, out uint multiPvValue):
					multiPv = multiPvValue;
					i++;
					break;
				case "nodes" when TryReadUInt(tokens, i + 1, out uint nodesValue):
					nodes = nodesValue;
					i++;
					break;
				case "nps" when TryReadUInt(tokens, i + 1, out uint npsValue):
					nps = npsValue;
					i++;
					break;
				case "tbhits" when TryReadUInt(tokens, i + 1, out uint tbHitsValue):
					tbHits = tbHitsValue;
					i++;
					break;
				case "time" when TryReadUInt(tokens, i + 1, out uint timeValue):
					time = timeValue;
					i++;
					break;
				case "hashfull" when TryReadUInt(tokens, i + 1, out uint hashFullValue):
					hashFull = hashFullValue;
					i++;
					break;
				case "cpuload" when TryReadUInt(tokens, i + 1, out uint cpuLoadValue):
					cpuLoad = cpuLoadValue;
					i++;
					break;
				case "currmove" when i + 1 < tokens.Length:
					currentMove = tokens[i + 1];
					i++;
					break;
				case "currmovenumber" when TryReadUInt(tokens, i + 1, out uint currentMoveNumberValue):
					currentMoveNumber = currentMoveNumberValue;
					i++;
					break;
				case "score" when i + 2 < tokens.Length:
					{
						string type = tokens[i + 1];
						string rawValue = tokens[i + 2];
						i += 2;

						int? centipawns = null;
						int? mate = null;
						if (type.Equals("cp", StringComparison.OrdinalIgnoreCase) &&
							int.TryParse(rawValue, out int cpValue))
							centipawns = cpValue;
						else if (type.Equals("mate", StringComparison.OrdinalIgnoreCase) &&
								 int.TryParse(rawValue, out int mateValue))
							mate = mateValue;

						var bound = UciScoreBound.Exact;
						if (i + 1 < tokens.Length)
						{
							if (tokens[i + 1].Equals("lowerbound", StringComparison.OrdinalIgnoreCase))
							{
								bound = UciScoreBound.Lower;
								i++;
							}
							else if (tokens[i + 1].Equals("upperbound", StringComparison.OrdinalIgnoreCase))
							{
								bound = UciScoreBound.Upper;
								i++;
							}
						}

						score = new(centipawns, mate, bound);
						break;
					}
				case "refutation":
					refutation = ReadTokenSequence(tokens, ref i);
					break;
				case "currline":
					{
						int startIndex = i + 1;
						if (TryReadUInt(tokens, startIndex, out uint cpuValue))
						{
							currentLineCpu = cpuValue;
							startIndex++;
						}

						currentLine = ReadTokenSequence(tokens, ref i, startIndex);
						break;
					}
				case "string":
					infoString = string.Join(" ", tokens, i + 1, tokens.Length - (i + 1));
					i = tokens.Length;
					break;
				case "pv":
					{
						var pvMoves = new List<string>();
						for (int j = i + 1; j < tokens.Length; j++)
							pvMoves.Add(tokens[j]);

						if (pvMoves.Count > 0)
							principalVariation = new(
								depth ?? 0,
								selDepth ?? 0,
								multiPv ?? 0,
								score?.Centipawns,
								score?.Mate,
								nodes ?? 0,
								nps ?? 0,
								tbHits ?? 0,
								time ?? 0,
								pvMoves.ToArray(),
								string.Join(" ", pvMoves)
							);

						i = tokens.Length;
						break;
					}
			}
		}

		message = new(
			new(
				depth,
				selDepth,
				multiPv,
				score,
				nodes,
				nps,
				tbHits,
				time,
				hashFull,
				cpuLoad,
				currentMove,
				currentMoveNumber,
				refutation ?? (IReadOnlyList<string>)Array.Empty<string>(),
				currentLineCpu,
				currentLine ?? (IReadOnlyList<string>)Array.Empty<string>(),
				infoString,
				principalVariation
			),
			line
		);

		return true;
	}

	private static bool TryParseProtection<TMessage>(
		string                                line,
		string                                keyword,
		Func<UciProtectionState, string, TMessage> factory,
		out TMessage?                         message)
		where TMessage : UciProtocolMessage
	{
		string prefix = keyword + " ";
		if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			message = null;
			return false;
		}

		string payload = line[prefix.Length..].Trim();
		if (!TryParseProtectionState(payload, out var state))
		{
			message = null;
			return false;
		}

		message = factory(state, line);
		return true;
	}

	private static bool TryParseProtectionState(string value, out UciProtectionState state)
	{
		switch (value.ToLowerInvariant())
		{
			case "checking":
				state = UciProtectionState.Checking;
				return true;
			case "ok":
				state = UciProtectionState.Ok;
				return true;
			case "error":
				state = UciProtectionState.Error;
				return true;
			default:
				state = default;
				return false;
		}
	}

	private static bool TryReadUInt(string[] tokens, int index, out uint value)
	{
		value = 0;
		return index < tokens.Length && uint.TryParse(tokens[index], out value);
	}

	private static List<string> ReadTokenSequence(string[] tokens, ref int currentIndex) =>
		ReadTokenSequence(tokens, ref currentIndex, currentIndex + 1);

	private static List<string> ReadTokenSequence(string[] tokens, ref int currentIndex, int startIndex)
	{
		var values = new List<string>();
		int index = startIndex;
		while (index < tokens.Length && !IsInfoKeyword(tokens[index]))
		{
			values.Add(tokens[index]);
			index++;
		}

		currentIndex = index - 1;
		return values;
	}

	private static bool IsInfoKeyword(string token) =>
		token is
			"depth" or
			"seldepth" or
			"multipv" or
			"score" or
			"nodes" or
			"nps" or
			"tbhits" or
			"time" or
			"hashfull" or
			"cpuload" or
			"currmove" or
			"currmovenumber" or
			"refutation" or
			"currline" or
			"pv" or
			"string";
}
