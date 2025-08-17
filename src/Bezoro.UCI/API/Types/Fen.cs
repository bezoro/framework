using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.Domain.Common.Constants;

namespace Bezoro.UCI.API.Types;

public readonly record struct Fen
{
	private const char   FenSeparator = ':';
	private const string FenKeyword   = "fen";

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

	public char     ActiveColor     { get; }
	public int      FullmoveNumber  { get; }
	public int      HalfmoveClock   { get; }
	public string   CastlingRights  { get; }
	public string   EnPassantTarget { get; }
	public string   PiecePlacement  { get; }
	public string   Raw             { get; }
	public string?  Checkers        { get; }
	public string[] FenParts        { get; }

	public static implicit operator string(Fen fen) => fen.Raw;

	public static bool TryParseUciOutputLine(string line, out Fen? fen)
	{
		fen = null;
		if (line.IsNullOrEmpty()) return false;

		string trimmed = line.Trim();
		if (trimmed.IsEmpty()) return false;

		if (!trimmed.StartsWith(FenKeyword, StringComparison.OrdinalIgnoreCase)) return false;

		try
		{
			string fenPayload = ExtractFenPayloadFromUciLine(trimmed);
			fen = Parse(fenPayload);
			return true;
		}
		catch
		{
			fen = null;
			return false;
		}
	}

	public static bool Validate(string rawFen)
	{
		if (rawFen.IsNullOrEmpty()) return false;

		string[] parts = rawFen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 6) return false;

		foreach (string part in parts)
		{
			if (part.IsNullOrEmpty())
				return false;
		}

		return true;
	}

	public static Fen Default() => Parse(UciConstants.STANDARD_FEN)!.Value;

	public static Fen Empty() => new();

	public static Fen? Parse(string rawFen)
	{
		if (rawFen.IsNullOrEmpty()) return null;

		string[] parts = rawFen.Split([' '], StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 6) return null;

		string piecePlacement = parts[0].ThrowIfEmpty();

		char activeColor = ParseActiveColor(parts);

		string castlingRights  = parts.Length > 2 ? parts[2].ThrowIfNull() : string.Empty;
		string enPassantTarget = parts.Length > 3 ? parts[3].ThrowIfNull() : string.Empty;

		var moveCounters = ParseMoveCounters(parts);

		string checkers = ParseCheckers(parts);

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

	public override string ToString() => Raw;

	private static (int halfmoveClock, int fullmoveNumber) ParseMoveCounters(string[] parts)
	{
		var halfmoveClock = 0;
		if (parts.Length > 4)
		{
			parts[4].ThrowIfNull().ThrowIfEmpty();
			int.TryParse(parts[4], out halfmoveClock);
		}

		var fullmoveNumber = 1;
		if (parts.Length > 5)
		{
			parts[5].ThrowIfNull().ThrowIfEmpty();
			int.TryParse(parts[5], out fullmoveNumber);
		}

		return (halfmoveClock, fullmoveNumber);
	}

	private static char ParseActiveColor(string[] parts)
	{
		parts[1].ThrowIfEmpty();
		var c = char.ToLowerInvariant(parts[1][0]);
		return c is 'w' or 'b'
				   ? c
				   : throw new ArgumentException("Invalid active color in FEN. Expected 'w' or 'b'.", nameof(parts));
	}

	private static string ExtractFenPayloadFromUciLine(string line) =>
		line is [_, _, _, FenSeparator, ..]
			? line[4..].TrimStart()
			: line[3..].TrimStart();

	private static string ParseCheckers(string[] parts)
	{
		if (parts.Length <= 6) return string.Empty;

		for (var i = 6; i < parts.Length; i++) parts[i].ThrowIfNull();
		return string.Join(" ", parts, 6, parts.Length - 6);
	}
}
