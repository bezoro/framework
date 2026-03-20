using Bezoro.Core.Extensions;
using Bezoro.Chess.UCI.Protocol.Domain.Common.Constants;
using Bezoro.Chess.UCI.Protocol.Domain.Common.Helpers;

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
		string[] fenParts,
		string?  checkers,
		string   raw)
	{
		PiecePlacement  = piecePlacement;
		ActiveColor     = activeColor;
		CastlingRights  = castlingRights;
		EnPassantTarget = enPassantTarget;
		HalfmoveClock   = halfmoveClock;
		FullmoveNumber  = fullmoveNumber;
		FenParts        = fenParts;
		Checkers        = checkers;
		Raw             = raw;
	}

	/// <summary>
	///     Gets the standard chess starting position.
	/// </summary>
	public static Fen Default => Parse(UciConstants.Fen.STANDARD)!.Value;

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
	///     Gets the whitespace-delimited FEN parts.
	/// </summary>
	public string[] FenParts        { get; }

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
				last.FenParts,
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
		!string.IsNullOrWhiteSpace(rawFen) && UciHelper.IsValidFen(rawFen.Trim());

	/// <summary>
	///     Returns an empty/default FEN value.
	/// </summary>
	public static Fen Empty() => new();

	/// <summary>
	///     Parses a raw FEN string into a <see cref="Fen" /> value.
	/// </summary>
	/// <param name="rawFen">Raw FEN text to parse.</param>
	/// <returns>The parsed value, or <see langword="null" /> when validation fails.</returns>
	public static Fen? Parse(string rawFen)
	{
		if (!Validate(rawFen)) return null;

		string[] parts = rawFen.Split(' ', StringSplitOptions.RemoveEmptyEntries);

		string piecePlacement = parts[0].ThrowIfEmpty();

		char activeColor = ParseActiveColor(parts);

		string castlingRights  = parts.Length > 2 ? parts[2].ThrowIfNull() : string.Empty;
		string enPassantTarget = parts.Length > 3 ? parts[3].ThrowIfNull() : string.Empty;

		var    moveCounters = ParseMoveCounters(parts);
		string checkers     = ParseCheckers(parts);

		return new(
			piecePlacement,
			activeColor,
			castlingRights,
			enPassantTarget,
			moveCounters.halfmoveClock,
			moveCounters.fullmoveNumber,
			parts,
			checkers,
			rawFen
		);
	}

	/// <summary>
	///     Returns the raw FEN string.
	/// </summary>
	public override string ToString() => Raw;

	private static (int halfmoveClock, int fullmoveNumber) ParseMoveCounters(string[] parts)
	{
		var halfmoveClock = 0;
		if (parts.Length > 4)
		{
			parts[4].ThrowIfNull().ThrowIfEmpty();
			if (!int.TryParse(parts[4], out halfmoveClock))
				throw new ArgumentException("Halfmove clock in FEN must be an integer.", nameof(parts));
		}

		var fullmoveNumber = 1;
		if (parts.Length > 5)
		{
			parts[5].ThrowIfNull().ThrowIfEmpty();
			if (!int.TryParse(parts[5], out fullmoveNumber))
				throw new ArgumentException("Fullmove number in FEN must be an integer.", nameof(parts));
		}

		return (halfmoveClock, fullmoveNumber);
	}

	private static char ParseActiveColor(string[] parts)
	{
		if (parts.Length < 2) throw new ArgumentException("FEN does not specify active color.", nameof(parts));

		string token = parts[1].ThrowIfEmpty();
		if (token.Length != 1)
			throw new ArgumentException(
				"Invalid active color in FEN. Expected single character 'w' or 'b'.",
				nameof(parts)
			);

		var c = char.ToLowerInvariant(token[0]);
		return c switch
		{
			'w' => 'w',
			'b' => 'b',
			_   => throw new ArgumentException("Invalid active color in FEN. Expected 'w' or 'b'.", nameof(parts))
		};
	}

	private static string ExtractFenPayloadFromUciLine(string line)
	{
		string payload                                                 = line[FEN_KEYWORD.Length..].TrimStart();
		if (payload.Length > 0 && payload[0] == FEN_SEPARATOR) payload = payload[1..].TrimStart();
		return payload;
	}

	private static string ParseCheckers(string[] parts)
	{
		if (parts.Length <= 6) return string.Empty;

		for (var i = 6; i < parts.Length; i++) parts[i].ThrowIfNull();
		return string.Join(" ", parts, 6, parts.Length - 6);
	}
}
