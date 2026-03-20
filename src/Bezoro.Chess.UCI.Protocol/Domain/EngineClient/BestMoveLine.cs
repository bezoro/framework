namespace Bezoro.Chess.UCI.Protocol.Domain.EngineClient;

/// <summary>
///     Represents a parsed "bestmove" line emitted by a UCI engine.
/// </summary>
internal readonly record struct BestMoveLine(string BestMove, string? PonderMove)
{
	public bool HasPonder => !string.IsNullOrEmpty(PonderMove);

	/// <summary>
	///     Attempts to parse a "bestmove" output line.
	/// </summary>
	public static bool TryParse(string line, out BestMoveLine result)
	{
		result = default;
		if (string.IsNullOrWhiteSpace(line)) return false;

		var span = line.AsSpan();
		if (!span.StartsWith("bestmove ", StringComparison.OrdinalIgnoreCase)) return false;

		var     remainder = span[9..];
		string? best      = ReadToken(ref remainder);
		if (string.IsNullOrEmpty(best)) return false;

		string? ponder = null;

		SkipWhitespace(ref remainder);
		if (remainder.Length >= 6 && StartsWithPonder(remainder))
		{
			remainder = remainder[6..];
			SkipWhitespace(ref remainder);
			ponder = ReadToken(ref remainder);
			if (string.IsNullOrEmpty(ponder))
				ponder = null;
		}

		result = new(best, ponder);
		return true;
	}

	private static bool StartsWithPonder(ReadOnlySpan<char> span)
	{
		if (span.Length < 6) return false;

		return (span[0] | 0x20) == 'p' &&
			   (span[1] | 0x20) == 'o' &&
			   (span[2] | 0x20) == 'n' &&
			   (span[3] | 0x20) == 'd' &&
			   (span[4] | 0x20) == 'e' &&
			   (span[5] | 0x20) == 'r';
	}

	private static string? ReadToken(ref ReadOnlySpan<char> span)
	{
		SkipWhitespace(ref span);
		var i = 0;
		while (i < span.Length && span[i] != ' ') i++;
		if (i == 0) return null;

		string token = new(span[..i]);
		span = span[i..];
		return token;
	}

	private static void SkipWhitespace(ref ReadOnlySpan<char> span)
	{
		var i = 0;
		while (i < span.Length && span[i] == ' ') i++;
		span = span[i..];
	}
}
