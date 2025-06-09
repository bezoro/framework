using System;
using System.Runtime.CompilerServices;
using System.Text;
using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Common.Helpers
{
	/// <summary>
	///     High-performance, fully validated FEN parser / formatter.
	/// </summary>
	public static class FenUtils
	{
		public const string EMPTY_FEN          = "8/8/8/8/8/8/8/8";
		public const string START_ACTIVE_COLOR = " w";
		public const string START_CASTLING     = " KQkq";
		public const string START_EN_PASSANT   = " -";
		public const string START_FEN = START_PIECES       +
										START_ACTIVE_COLOR +
										START_CASTLING     +
										START_EN_PASSANT   +
										START_HALF_MOVE    +
										START_FULL_MOVE;
		public const string START_FULL_MOVE = " 1";
		public const string START_HALF_MOVE = " 0";
		public const string START_PIECES =
			"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR";
		public static FenData StartBoard { get; } = Parse(START_FEN);

		public static string EmptyBoard  => EMPTY_FEN;
		public static string StartPieces => START_PIECES;

		public static bool IsValidPiecePlacement(string field)
		{
			string[] ranks = field.Split('/');
			if (ranks.Length != 8) return false;

			foreach (var rank in ranks)
			{
				var files = 0;
				foreach (var c in rank)
				{
					if (char.IsDigit(c)) files                     += c - '0';
					else if ("prnbqkPRNBQK".IndexOf(c) >= 0) files += 1;
					else return false;
				}

				if (files != 8) return false;
			}

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryParse(string? fen, out FenData data, out string? error)
		{
			data  = default;
			error = null;

			if (string.IsNullOrWhiteSpace(fen))
			{
				error = "FEN string is null or blank.";
				return false;
			}

			// Split the FEN string by spaces, removing empty entries.
			var parts = fen.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

			if (parts.Length is 0 or > 6)
			{
				error = "FEN string must contain between 1 and 6 space-separated fields.";
				return false;
			}

			// Field 1: Piece Placement (Mandatory)
			var piecePlacement = parts[0];
			if (!IsValidPiecePlacement(piecePlacement)) // Assuming FenValidators exists
			{
				error = "Invalid piece-placement section.";
				return false;
			}

			// Field 2: Active Color (Default: 'w')
			var activeColorStr = parts.Length > 1 ? parts[1] : "w";
			if (!TryParseColor(activeColorStr, out var color))
			{
				error = "Active color must be 'w' or 'b'.";
				return false;
			}

			// Field 3: Castling Availability (Default: '-')
			var castlingStr = parts.Length > 2 ? parts[2] : "-";
			if (!TryParseCastling(castlingStr, out var rights))
			{
				error = "Malformed castling section.";
				return false;
			}

			// Field 4: En Passant Target Square (Default: '-')
			var enPassantStr = parts.Length > 3 ? parts[3] : "-";
			// Assuming FenValidators.TryParseEnPassant validates and outputs the string representation (e.g. "e3" or "-")
			if (!TryParseEnPassant(enPassantStr, out var epSquareString))
			{
				error = "Invalid en-passant target square.";
				return false;
			}

			// Field 5: Halfmove Clock (Default: 0)
			var halfmoveClockStr = parts.Length > 4 ? parts[4] : "0";
			if (!int.TryParse(halfmoveClockStr, out var half) || half < 0)
			{
				error = "Half-move clock must be a non-negative integer.";
				return false;
			}

			// Field 6: Fullmove Number (Default: 1)
			var fullmoveNumberStr = parts.Length > 5 ? parts[5] : "1";
			if (!int.TryParse(fullmoveNumberStr, out var full) || full < 1)
			{
				error = "Full-move number must be a positive integer (>= 1).";
				return false;
			}

			data = new(piecePlacement, color, rights, epSquareString, half, full);
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryParseCastling(string token, out CastlingRights rights)
		{
			rights = CastlingRights.None;
			if (token == "-") return true;

			// Ensure no invalid characters and build rights
			var tempRights = CastlingRights.None;
			foreach (var c in token)
			{
				switch (c)
				{
					case 'K': tempRights |= CastlingRights.WhiteKingSide; break;
					case 'Q': tempRights |= CastlingRights.WhiteQueenSide; break;
					case 'k': tempRights |= CastlingRights.BlackKingSide; break;
					case 'q': tempRights |= CastlingRights.BlackQueenSide; break;
					default:  return false; // Invalid character
				}
			}

			// Check for duplicates or invalid order if necessary, though FEN standard is somewhat loose here.
			// For simplicity, we accept any combination of valid characters.
			rights = tempRights;
			return true; // True if token was "-" or only contained valid castling characters
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryParseColor(string token, out PlayerColor color)
		{
			color = PlayerColor.None; // Default to an invalid state
			if (token.Length != 1)
				return false;

			switch (token[0])
			{
				case 'w':
					color = PlayerColor.White;
					return true;
				case 'b':
					color = PlayerColor.Black;
					return true;
			}

			return false;
		}

		public static bool TryParseEnPassant(string token, out string square)
		{
			square = token;
			if (token == "-") return true;

			return token.Length == 2 && "abcdefgh".Contains(token[0]) && "36".Contains(token[1]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char ToFenChar(this PlayerColor color) =>
			color == PlayerColor.White ? 'w' : 'b';

		/// <exception cref="ArgumentException">Malformed FEN.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FenData Parse(string fen)
		{
			if (!TryParse(fen, out var data, out var err))
				throw new ArgumentException(err ?? "Invalid FEN string.", nameof(fen));

			return data;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string Format(in FenData fen) =>
			string.Concat(
				fen.PiecePlacement, " ",
				fen.ActiveColor.ToFenChar(), " ",
				fen.Castling.ToFenString(), " ",
				fen.EnPassant, " ", // FenData.EnPassant should be the string representation
				fen.HalfmoveClock.ToString(), " ",
				fen.FullmoveNumber.ToString());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToFenString(this CastlingRights rights)
		{
			if (rights == CastlingRights.None) return "-";
			var sb = new StringBuilder(4);
			if (rights.HasFlag(CastlingRights.WhiteKingSide)) sb.Append('K');
			if (rights.HasFlag(CastlingRights.WhiteQueenSide)) sb.Append('Q');
			if (rights.HasFlag(CastlingRights.BlackKingSide)) sb.Append('k');
			if (rights.HasFlag(CastlingRights.BlackQueenSide)) sb.Append('q');
			return sb.ToString();
		}
	}

	public readonly struct FenData
	{
		public FenData(
			string piecePlacement,
			PlayerColor activeColor,
			CastlingRights castling,
			string enPassant,
			int halfmoveClock,
			int fullmoveNumber)
		{
			PiecePlacement = piecePlacement;
			ActiveColor    = activeColor;
			Castling       = castling;
			EnPassant      = enPassant;
			HalfmoveClock  = halfmoveClock;
			FullmoveNumber = fullmoveNumber;
			FullString     = $"{piecePlacement} {activeColor} {castling} {enPassant} {halfmoveClock} {fullmoveNumber}";
		}

		public readonly CastlingRights Castling;
		public readonly int            FullmoveNumber;
		public readonly int            HalfmoveClock;
		public readonly PlayerColor    ActiveColor;
		public readonly string         EnPassant;
		public readonly string         FullString;
		public readonly string         PiecePlacement;
	}
}
