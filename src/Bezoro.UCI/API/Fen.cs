using Bezoro.Core.Common.Extensions;

namespace Bezoro.UCI.API;

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
	public static bool TryParse(string line, out Fen fen)
	{
		line.ThrowIfNull();

		fen = default;
		string raw = line.Trim();

		if (raw.Length == 0) return false;
		if (!raw.StartsWith("fen", StringComparison.OrdinalIgnoreCase)) return false;

		if (raw.Length >= 4 && raw[3] == ':')
			raw = raw[4..].TrimStart();
		else
			raw = raw[3..].TrimStart();

		string[] parts = raw.Split([' '], StringSplitOptions.RemoveEmptyEntries);

		string piecePlacement = parts.Length > 0 ? parts[0] : string.Empty;

		var activeColor = '\0';
		if (parts.Length > 1 && parts[1].Length > 0)
		{
			var c = char.ToLowerInvariant(parts[1][0]);
			activeColor = c is 'w' or 'b' ? c : '\0';
		}

		string castlingRights  = parts.Length > 2 ? parts[2] : string.Empty;
		string enPassantTarget = parts.Length > 3 ? parts[3] : string.Empty;

		var halfmoveClock = 0;
		if (parts.Length > 4) int.TryParse(parts[4], out halfmoveClock);

		var fullmoveNumber = 1;
		if (parts.Length > 5) int.TryParse(parts[5], out fullmoveNumber);

		string checkers                = string.Empty;
		if (parts.Length > 6) checkers = string.Join(" ", parts, 6, parts.Length - 6);

		fen = new(
			piecePlacement,
			activeColor,
			castlingRights,
			enPassantTarget,
			halfmoveClock,
			fullmoveNumber,
			parts,
			checkers,
			raw);

		return true;
	}
}

public readonly record struct Position(string Notation, Piece? Piece);
