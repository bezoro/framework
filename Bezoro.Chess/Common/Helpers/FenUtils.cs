using System;
using System.Diagnostics;
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

		static FenUtils()
		{
			try
			{
				StartBoard = Parse(START_FEN);
			}
			catch (Exception ex)
			{
				Debug.Fail($"Failed to initialise StartBoard with START_FEN: {ex}");
				StartBoard = default;
			}
		}

		public static FenData            StartBoard          { get; }
		public static ReadOnlySpan<char> EmptyPiecePlacement => EMPTY_FEN.AsSpan();
		public static ReadOnlySpan<char> StartPiecePlacement => START_PIECES.AsSpan();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsValidPiecePlacement(ReadOnlySpan<char> field)
		{
			if (field.Length > 71) return false;

			var ranks = 0;
			var files = 0;

			foreach (var c in field)
			{
				if (c == '/')
				{
					if (files != 8) return false;
					files = 0;
					ranks++;
					continue;
				}

				if (char.IsDigit(c))
				{
					var emptySquares = c - '0';
					if (emptySquares < 1 || emptySquares > 8) return false;
					files += emptySquares;
				}
				else
				{
					switch (c)
					{
						case 'p':
						case 'r':
						case 'n':
						case 'b':
						case 'q':
						case 'k':
						case 'P':
						case 'R':
						case 'N':
						case 'B':
						case 'Q':
						case 'K':
							files += 1;
							break;

						default:
							return false;
					}
				}

				if (files > 8) return false; // Too many files in a rank
			}

			return ranks == 7 && files == 8; // 7 slashes + final rank = 8 ranks total
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryParse(string? fen, out FenData data, out string? error)
		{
			data  = default;
			error = null;

			if (string.IsNullOrWhiteSpace(fen))
			{
				error = "FEN string cannot be null or empty.";
				return false;
			}

			return TryParse(fen.AsSpan(), out data, out error);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryParse(ReadOnlySpan<char> fen, out FenData data, out string? error)
		{
			data  = default;
			error = null;

			if (fen.IsEmpty || fen.IsWhiteSpace())
			{
				error = "FEN string cannot be null or empty.";
				return false;
			}

			Span<Range> parts     = stackalloc Range[6];
			var         partCount = SplitFenString(fen, parts);

			// Strict FEN: must have exactly six space-separated fields
			if (partCount != 6)
			{
				error = "FEN string must contain exactly six space-separated fields.";
				return false;
			}

			// Field 1: Piece Placement
			var piecePlacement = fen[parts[0]];
			if (!IsValidPiecePlacement(piecePlacement))
			{
				error = "Invalid piece placement section.";
				return false;
			}

			// Field 2: Active Color
			var activeColorSpan = fen[parts[1]];
			if (!TryParseColor(activeColorSpan, out var color))
			{
				error = "Active color must be 'w' (white) or 'b' (black).";
				return false;
			}

			// Field 3: Castling Availability
			var castlingSpan = fen[parts[2]];
			if (!TryParseCastling(castlingSpan, out var rights))
			{
				error = "Invalid castling availability section.";
				return false;
			}

			// Field 4: En Passant Target Square
			var enPassantSpan = fen[parts[3]];
			if (!TryParseEnPassant(enPassantSpan, out var epSquareString))
			{
				error = "Invalid en passant target square.";
				return false;
			}

			// Field 5: Halfmove Clock
			var halfmoveClockSpan = fen[parts[4]];
			if (!TryParseInt(halfmoveClockSpan, out var half) || half < 0)
			{
				error = "Halfmove clock must be a non-negative integer.";
				return false;
			}

			// Field 6: Fullmove Number
			var fullmoveNumberSpan = fen[parts[5]];
			if (!TryParseInt(fullmoveNumberSpan, out var full) || full < 1)
			{
				error = "Fullmove number must be a positive integer (>= 1).";
				return false;
			}

			data = new(piecePlacement.ToString(), color, rights, epSquareString, half, full);
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryParseCastling(ReadOnlySpan<char> token, out CastlingRights rights)
		{
			rights = CastlingRights.None;

			if (token.Length == 1 && token[0] == '-')
				return true;

			if (token.Length > 4)
				return false;

			var tempRights = CastlingRights.None;
			var seenFlags  = 0; // Bit flags to track duplicates: K=1, Q=2, k=4, q=8

			foreach (var c in token)
			{
				int            flag;
				CastlingRights right;

				switch (c)
				{
					case 'K':
						flag  = 1;
						right = CastlingRights.WhiteKingSide;
						break;
					case 'Q':
						flag  = 2;
						right = CastlingRights.WhiteQueenSide;
						break;
					case 'k':
						flag  = 4;
						right = CastlingRights.BlackKingSide;
						break;
					case 'q':
						flag  = 8;
						right = CastlingRights.BlackQueenSide;
						break;
					default:
						return false; // Invalid character
				}

				// Check for duplicates
				if ((seenFlags & flag) != 0)
					return false;

				seenFlags  |= flag;
				tempRights |= right;
			}

			rights = tempRights;
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryParseColor(ReadOnlySpan<char> token, out PlayerColor color)
		{
			color = PlayerColor.None;

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
				default:
					return false;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryParseEnPassant(ReadOnlySpan<char> token, out string square)
		{
			if (token.Length == 1 && token[0] == '-')
			{
				square = "-";
				return true;
			}

			if (token.Length != 2)
			{
				square = string.Empty;
				return false;
			}

			var file = token[0];
			var rank = token[1];

			// File must be a-h
			if (file < 'a' || file > 'h')
			{
				square = string.Empty;
				return false;
			}

			// Rank must be 3 or 6 (valid en passant target squares)
			if (rank != '3' && rank != '6')
			{
				square = string.Empty;
				return false;
			}

			square = token.ToString();
			return true;
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

		/// <summary>
		///     High-performance integer parsing for spans
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool TryParseInt(ReadOnlySpan<char> span, out int result)
		{
			result = 0;

			if (span.IsEmpty)
				return false;

			var isNegative = false;
			var startIndex = 0;

			// Handle negative numbers
			if (span[0] == '-')
			{
				if (span.Length == 1)
					return false;

				isNegative = true;
				startIndex = 1;
			}
			else if (span[0] == '+')
			{
				if (span.Length == 1)
					return false;

				startIndex = 1;
			}

			// Parse digits
			for (var i = startIndex ; i < span.Length ; i++)
			{
				var c = span[i];
				if (c is < '0' or > '9')
					return false;

				var digit = c - '0';

				// Check for overflow before multiplication
				if (result > (int.MaxValue - digit) / 10)
					return false;

				result = result * 10 + digit;
			}

			if (isNegative)
				result = -result;

			return true;
		}

		/// <summary>
		///     Splits a FEN string by spaces, returning the number of parts found.
		/// </summary>
		/// <param name="fen">The FEN string to split</param>
		/// <param name="parts">Span to store the ranges of each part</param>
		/// <returns>Number of parts found</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int SplitFenString(ReadOnlySpan<char> fen, Span<Range> parts)
		{
			var partCount = 0;
			var start     = 0;

			for (var i = 0 ; i < fen.Length && partCount < parts.Length ; i++)
			{
				if (fen[i] != ' ')
					continue;

				if (i > start) // Skip empty parts
				{
					parts[partCount] = new(start, i);
					partCount++;
				}

				// Skip consecutive spaces
				while (i < fen.Length - 1 && fen[i + 1] == ' ')
					i++;

				start = i + 1;
			}

			// Add the last part if it exists
			if (start >= fen.Length || partCount >= parts.Length)
				return partCount;

			parts[partCount] = new(start, fen.Length);
			partCount++;

			return partCount;
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
			_fullString    = null; // Lazy initialization
		}

		public readonly  CastlingRights Castling;
		public readonly  int            FullmoveNumber;
		public readonly  int            HalfmoveClock;
		public readonly  PlayerColor    ActiveColor;
		public readonly  string         EnPassant;
		public readonly  string         PiecePlacement;
		private readonly string?        _fullString;

		public string FullString => _fullString ?? FenUtils.Format(this);
	}
}
