using Bezoro.Core.Extensions;
using Bezoro.Chess.UCI.Protocol.Domain.Common.Constants;
using System.Globalization;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a parsed Forsyth-Edwards Notation (FEN) position and optional engine-specific checkers metadata.
/// </summary>
public readonly record struct Fen
{
	private const char   FEN_SEPARATOR    = ':';
	private const string CHECKERS_KEYWORD = "checkers";
	private const string FEN_KEYWORD      = "fen";

	private Fen(
		string   piecePlacement,
		char     activeColor,
		string   castlingRights,
		string   enPassantTarget,
		int      halfmoveClock,
		int      fullmoveNumber,
		string?  checkers,
		string   raw)
	{
		PiecePlacement  = piecePlacement;
		ActiveColor     = activeColor;
		CastlingRights  = castlingRights;
		EnPassantTarget = enPassantTarget;
		HalfmoveClock   = halfmoveClock;
		FullmoveNumber  = fullmoveNumber;
		Checkers        = checkers;
		Raw             = raw;
	}

	/// <summary>
	///     Gets the standard chess starting position.
	/// </summary>
	public static Fen Default { get; } = Parse(UciConstants.Fen.STANDARD)!.Value;

	/// <summary>
	///     Gets the active side to move, normalized to <c>w</c> or <c>b</c>.
	/// </summary>
	public char     ActiveColor     { get; }

	/// <summary>
	///     Gets the fullmove number component from the FEN.
	/// </summary>
	public int      FullmoveNumber  { get; }

	/// <summary>
	///     Gets the halfmove clock component from the FEN.
	/// </summary>
	public int      HalfmoveClock   { get; }

	/// <summary>
	///     Gets the castling-rights component from the FEN.
	/// </summary>
	public string   CastlingRights  { get; }

	/// <summary>
	///     Gets the en-passant target square component from the FEN.
	/// </summary>
	public string   EnPassantTarget { get; }

	/// <summary>
	///     Gets the board piece-placement component from the FEN.
	/// </summary>
	public string   PiecePlacement  { get; }

	/// <summary>
	///     Gets the raw FEN text used to create this value.
	/// </summary>
	public string   Raw             { get; }

	/// <summary>
	///     Gets the optional <c>checkers</c> payload reported by engines that support the non-standard <c>d</c> command.
	/// </summary>
	public string?  Checkers        { get; }

	/// <summary>
	///     Converts the value to its raw FEN representation.
	/// </summary>
	/// <param name="fen">FEN value to convert.</param>
	public static implicit operator string(Fen fen) => fen.Raw;

	/// <summary>
	///     Attempts to parse a UCI output line containing either a <c>fen</c> or <c>checkers</c> payload.
	/// </summary>
	/// <param name="line">Raw engine output line.</param>
	/// <param name="lastRawFen">Most recently observed raw FEN used to enrich a later <c>checkers</c> line.</param>
	/// <param name="fen">Parsed FEN value when successful.</param>
	/// <returns><see langword="true" /> when the line was recognized and parsed; otherwise <see langword="false" />.</returns>
	public static bool TryParseUciOutputLine(string line, ref string? lastRawFen, out Fen? fen)
	{
		fen = null;
		if (line.IsNullOrEmpty()) return false;

		string trimmed = line.Trim();
		if (trimmed.IsEmpty()) return false;

		// "fen[: ]<payload>"
		if (trimmed.StartsWith(FEN_KEYWORD, StringComparison.OrdinalIgnoreCase))
		{
			string fenPayload = ExtractFenPayloadFromUciLine(trimmed);
			var    parsed     = Parse(fenPayload);
			if (!parsed.HasValue) return false;

			fen        = parsed;
			lastRawFen = fenPayload;
			return true;
		}

		// "checkers[: ]<payload>"
		if (!trimmed.StartsWith(CHECKERS_KEYWORD, StringComparison.OrdinalIgnoreCase)) return false;

		string payload = trimmed[CHECKERS_KEYWORD.Length..].TrimStart();
		if (payload.Length > 0 && payload[0] == FEN_SEPARATOR)
			payload = payload[1..].TrimStart();

		if (string.IsNullOrEmpty(lastRawFen)) return false;

		var lastParsed = Parse(lastRawFen);
		if (lastParsed.HasValue)
		{
			var last = lastParsed.Value;
			var enriched = new Fen(
				last.PiecePlacement,
				last.ActiveColor,
				last.CastlingRights,
				last.EnPassantTarget,
				last.HalfmoveClock,
				last.FullmoveNumber,
				payload,
				last.Raw
			);

			// keep cached raw FEN; 'checkers' is a separate line in engine output
			fen = enriched;
			return true;
		}

		lastRawFen = null;

		return false;
	}

	/// <summary>
	///     Validates whether the supplied raw FEN string is syntactically valid.
	/// </summary>
	/// <param name="rawFen">Candidate raw FEN text.</param>
	/// <returns><see langword="true" /> when the value is valid; otherwise <see langword="false" />.</returns>
	public static bool Validate(string rawFen) =>
		TryParseCore(rawFen, false, out _);

	/// <summary>
	///     Returns an empty/default FEN value.
	/// </summary>
	public static Fen Empty() => new();

	/// <summary>
	///     Parses a raw FEN string into a <see cref="Fen" /> value.
	/// </summary>
	/// <param name="rawFen">Raw FEN text to parse.</param>
	/// <returns>The parsed value, or <see langword="null" /> when validation fails.</returns>
	public static Fen? Parse(string rawFen) => TryParseCore(rawFen, true, out var fen) ? fen : null;

	/// <summary>
	///     Returns the raw FEN string.
	/// </summary>
	public override string ToString() => Raw;

	private static bool TryParseCore(string? rawFen, bool createValue, out Fen fen)
	{
		fen = default;
		ReadOnlySpan<char> text = rawFen.AsSpan();
		var offset = 0;
		ReadOnlySpan<char> placement = ReadToken(text, ref offset);
		ReadOnlySpan<char> color = ReadToken(text, ref offset);
		ReadOnlySpan<char> castling = ReadToken(text, ref offset);
		ReadOnlySpan<char> enPassant = ReadToken(text, ref offset);
		ReadOnlySpan<char> halfmove = ReadToken(text, ref offset);
		ReadOnlySpan<char> fullmove = ReadToken(text, ref offset);
		if (!ReadToken(text, ref offset).IsEmpty ||
			!IsValidPlacement(placement) ||
			color.Length != 1 || color[0] is not ('w' or 'b') ||
			!IsValidCastling(castling) ||
			!(enPassant.SequenceEqual("-") ||
			  enPassant.Length == 2 && enPassant[0] is >= 'a' and <= 'h' && enPassant[1] is '3' or '6') ||
			!int.TryParse(halfmove, NumberStyles.None, CultureInfo.InvariantCulture, out int halfmoveClock) ||
			!int.TryParse(fullmove, NumberStyles.None, CultureInfo.InvariantCulture, out int fullmoveNumber) ||
			fullmoveNumber < 1)
			return false;

		if (createValue)
			fen = new Fen(placement.ToString(), color[0], castling.ToString(), enPassant.ToString(),
				halfmoveClock, fullmoveNumber, string.Empty, rawFen!);
		return true;
	}

	private static ReadOnlySpan<char> ReadToken(ReadOnlySpan<char> text, ref int offset)
	{
		while (offset < text.Length && text[offset] is ' ' or '\t')
			offset++;
		int start = offset;
		while (offset < text.Length && text[offset] is not (' ' or '\t'))
			offset++;
		return text.Slice(start, offset - start);
	}

	private static bool IsValidPlacement(ReadOnlySpan<char> placement)
	{
		var rankCount = 1;
		var fileCount = 0;
		var previousWasDigit = false;
		foreach (char symbol in placement)
		{
			if (symbol == '/')
			{
				if (fileCount != 8 || ++rankCount > 8)
					return false;
				fileCount = 0;
				previousWasDigit = false;
				continue;
			}

			if (symbol is >= '1' and <= '8')
			{
				if (previousWasDigit)
					return false;
				fileCount += symbol - '0';
				previousWasDigit = true;
			}
			else
			{
				if ("prnbqkPRNBQK".AsSpan().IndexOf(symbol) < 0)
					return false;
				fileCount++;
				previousWasDigit = false;
			}

			if (fileCount > 8)
				return false;
		}

		return rankCount == 8 && fileCount == 8;
	}

	private static bool IsValidCastling(ReadOnlySpan<char> castling)
	{
		if (castling.SequenceEqual("-"))
			return true;
		if (castling.IsEmpty)
			return false;
		var previousIndex = -1;
		foreach (char right in castling)
		{
			int index = "KQkq".AsSpan().IndexOf(right);
			if (index <= previousIndex)
				return false;
			previousIndex = index;
		}
		return true;
	}

	private static string ExtractFenPayloadFromUciLine(string line)
	{
		string payload                                                 = line[FEN_KEYWORD.Length..].TrimStart();
		if (payload.Length > 0 && payload[0] == FEN_SEPARATOR) payload = payload[1..].TrimStart();
		return payload;
	}
}
