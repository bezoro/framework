using System.Collections.Generic;

namespace Bezoro.UCI.API.Types;

/// <summary>
///     Represents a typed option advertised by a UCI engine during handshake.
/// </summary>
public readonly record struct UciEngineOption(
	string               Name,
	UciOptionType        Type,
	string?              DefaultValue,
	int?                 Min,
	int?                 Max,
	IReadOnlyList<string> Variables
)
{
	internal static bool TryParse(string line, out UciEngineOption option)
	{
		option = default;

		if (string.IsNullOrWhiteSpace(line) ||
			!line.StartsWith("option ", StringComparison.OrdinalIgnoreCase))
			return false;

		string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (tokens.Length < 3) return false;

		string? name = null;
		string? typeToken = null;
		string? defaultValue = null;
		int? min = null;
		int? max = null;
		var variables = new List<string>();

		var index = 1;
		while (index < tokens.Length)
		{
			switch (tokens[index])
			{
				case "name":
					index++;
					name = ConsumeValue(tokens, ref index);
					break;

				case "type":
					index++;
					typeToken = ConsumeValue(tokens, ref index);
					break;

				case "default":
					index++;
					defaultValue = ConsumeValue(tokens, ref index);
					break;

				case "min":
					index++;
					if (index < tokens.Length && int.TryParse(tokens[index], out int parsedMin))
					{
						min = parsedMin;
						index++;
					}

					break;

				case "max":
					index++;
					if (index < tokens.Length && int.TryParse(tokens[index], out int parsedMax))
					{
						max = parsedMax;
						index++;
					}

					break;

				case "var":
					index++;
					variables.Add(ConsumeValue(tokens, ref index));
					break;

				default:
					index++;
					break;
			}
		}

		if (string.IsNullOrWhiteSpace(name) ||
			!TryParseOptionType(typeToken, out var optionType))
			return false;

		option = new(
			name,
			optionType,
			defaultValue,
			min,
			max,
			variables.ToArray()
		);

		return true;
	}

	private static string ConsumeValue(string[] tokens, ref int index)
	{
		int start = index;
		while (index < tokens.Length && !IsOptionKeyword(tokens[index]))
			index++;

		return index <= start ? string.Empty : string.Join(" ", tokens, start, index - start);
	}

	private static bool IsOptionKeyword(string token) =>
		token is "name" or "type" or "default" or "min" or "max" or "var";

	private static bool TryParseOptionType(string? rawType, out UciOptionType optionType)
	{
		switch (rawType?.Trim().ToLowerInvariant())
		{
			case "check":
				optionType = UciOptionType.Check;
				return true;

			case "spin":
				optionType = UciOptionType.Spin;
				return true;

			case "string":
				optionType = UciOptionType.String;
				return true;

			case "button":
				optionType = UciOptionType.Button;
				return true;

			case "combo":
				optionType = UciOptionType.Combo;
				return true;

			default:
				optionType = default;
				return false;
		}
	}
}
