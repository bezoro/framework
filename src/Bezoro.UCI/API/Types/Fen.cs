using Bezoro.Core.Common.Extensions;

namespace Bezoro.UCI.API.Types;

public readonly record struct Fen(
	string   PiecePlacement,
	char     ActiveColor,
	string   CastlingRights,
	string   EnPassantTarget,
	int      HalfmoveClock,
	int      FullmoveNumber,
	string[] FenParts,
	string?  Checkers,
	string   Raw
)
{
	public static implicit operator string(Fen fen) => fen.Raw;

	public static bool TryParseUciOutputLine(string line, out Fen? fen)
	{
		line.ThrowIfNull();

		fen = null;
		string rawFen = line.Trim();

		if (rawFen.Length == 0) return false;
		if (!rawFen.StartsWith("fen", StringComparison.OrdinalIgnoreCase)) return false;

		if (rawFen.Length >= 4 && rawFen[3] == ':')
			rawFen = rawFen[4..].TrimStart();
		else
			rawFen = rawFen[3..].TrimStart();

		fen = Parse(rawFen);
		return true;
	}

	public static Fen Parse(string rawFen)
	{
		rawFen.ThrowIfNull().ThrowIfEmpty();

		string[] parts = rawFen.Split([' '], StringSplitOptions.RemoveEmptyEntries);
		parts.ThrowIfNull();
		parts.Length.ThrowIfLessThan(4);

		string piecePlacement = parts.Length > 0 ? parts[0] : string.Empty;
		piecePlacement.ThrowIfEmpty();

		var activeColor = '\0';
		parts[1].ThrowIfEmpty();
		if (parts.Length > 1 && parts[1].Length > 0)
		{
			var c = char.ToLowerInvariant(parts[1][0]);
			activeColor = c is 'w' or 'b' ? c : '\0';
		}

		activeColor.ThrowIf(ac => ac == '\0');

		string castlingRights  = parts.Length > 2 ? parts[2].ThrowIfNull() : string.Empty;
		string enPassantTarget = parts.Length > 3 ? parts[3].ThrowIfNull() : string.Empty;

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

		string checkers = string.Empty;
		if (parts.Length > 6)
		{
			for (var i = 6; i < parts.Length; i++) parts[i].ThrowIfNull();
			checkers = string.Join(" ", parts, 6, parts.Length - 6);
		}

		return new()
		{
			ActiveColor     = activeColor,
			CastlingRights  = castlingRights,
			Checkers        = checkers,
			EnPassantTarget = enPassantTarget,
			FenParts        = parts,
			FullmoveNumber  = fullmoveNumber,
			HalfmoveClock   = halfmoveClock,
			PiecePlacement  = piecePlacement,
			Raw             = rawFen
		};
	}

	public override string ToString() => Raw;
}
