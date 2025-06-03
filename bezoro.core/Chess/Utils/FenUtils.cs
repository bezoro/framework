using System;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Chess.Utils
{
	/// <summary>
	///     High-performance, fully-validated FEN parser / formatter.
	/// </summary>
	public static class FenUtility
	{
		/*──────────────────────────────────────────────────────────*/
		/*  Static FEN strings                                       */
		/*──────────────────────────────────────────────────────────*/
		private const string _EMPTY_FEN = "8/8/8/8/8/8/8/8 w - - 0 1";
		private const string _START_FEN = _START_PIECES + " w KQkq - 0 1";
		private const string _START_PIECES =
			"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR";

		public static FenData EmptyBoard  { get; } = Parse(_EMPTY_FEN);
		public static FenData StartBoard  { get; } = Parse(_START_FEN);
		public static string  StartPieces => _START_PIECES;

		public static bool TryParse(
			string? fen,
			out FenData data,
			out string? error)
		{
			data  = default;
			error = null;

			if (string.IsNullOrWhiteSpace(fen))
			{
				error = "FEN string is null or blank.";
				return false;
			}

			var fields = new string[6];
			if (!fen.TrySplitIntoSixTokens(fields))
			{
				error = "FEN must contain exactly six space-separated fields.";
				return false;
			}

			if (!FenValidators.IsValidPiecePlacement(fields[0]))
			{
				error = "Invalid piece-placement section.";
				return false;
			}

			if (!FenExtensions.TryParseColor(fields[1], out var color))
			{
				error = "Active color must be 'w' or 'b'.";
				return false;
			}

			if (!FenExtensions.TryParseCastling(fields[2], out var rights))
			{
				error = "Malformed castling section.";
				return false;
			}

			if (!FenValidators.TryParseEnPassant(fields[3], out var ep))
			{
				error = "Invalid en-passant target square.";
				return false;
			}

			if (!int.TryParse(fields[4], out var half) || half < 0)
			{
				error = "Half-move clock must be ≥ 0.";
				return false;
			}

			if (!int.TryParse(fields[5], out var full) || full < 1)
			{
				error = "Full-move number must be ≥ 1.";
				return false;
			}

			data = new(fields[0], color, rights, ep, half, full);
			return true;
		}

		/*──────────────────────────────────────────────────────────*/
		/*  Public API                                              */
		/*──────────────────────────────────────────────────────────*/

		/// <exception cref="ArgumentException">Malformed FEN.</exception>
		public static FenData Parse(string fen)
		{
			if (!TryParse(fen, out var data, out var err))
				throw new ArgumentException(err, nameof(fen));

			return data;
		}

		public static string Format(in FenData fen) =>
			string.Concat(
				fen.PiecePlacement, " ",
				fen.ActiveColor.ToFenChar(), " ",
				fen.Castling.ToFenString(), " ",
				fen.EnPassant, " ",
				fen.HalfmoveClock.ToString(), " ",
				fen.FullmoveNumber.ToString());
	}

	/*──────────────────────────────────────────────────────────────*/
	/*  Internal helpers                                            */
	/*──────────────────────────────────────────────────────────────*/

	internal static class FenExtensions
	{
		/*——— CastlingRights ———*/
		public static bool TryParseCastling(string token, out CastlingRights rights)
		{
			rights = CastlingRights.None;
			if (token == "-") return true;

			foreach (var c in token)
			{
				rights |= c == 'K' ? CastlingRights.WhiteKingSide
					: c     == 'Q' ? CastlingRights.WhiteQueenSide
					: c     == 'k' ? CastlingRights.BlackKingSide
					: c     == 'q' ? CastlingRights.BlackQueenSide
									 : CastlingRights.None;
			}

			return rights != CastlingRights.None || token == "-";
		}

		public static bool TryParseColor(string token, out PlayerColor color)
		{
			color = token == "w" ? PlayerColor.White
				: token   == "b" ? PlayerColor.Black
								   : PlayerColor.None;

			return color != PlayerColor.None;
		}

		/*——— Tokeniser ———*/
		public static bool TrySplitIntoSixTokens(this string src, Span<string> dest)
		{
			int idx = 0, start = 0;
			for (var i = 0 ; i < src.Length ; i++)
			{
				if (src[i] == ' ')
				{
					if (idx == 6) return false; // too many
					dest[idx++] = src.Substring(start, i - start);
					start       = i + 1;
				}
			}

			if (idx != 5) return false; // too few
			dest[5] = src.Substring(start);
			return true;
		}

		/*——— PlayerColor ———*/
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char ToFenChar(this PlayerColor color) =>
			color == PlayerColor.White ? 'w' : 'b';

		public static string ToFenString(this CastlingRights rights) =>
			rights == CastlingRights.None
				? "-"
				: string.Concat(
					rights.HasFlag(CastlingRights.WhiteKingSide) ? "K" : string.Empty,
					rights.HasFlag(CastlingRights.WhiteQueenSide) ? "Q" : string.Empty,
					rights.HasFlag(CastlingRights.BlackKingSide) ? "k" : string.Empty,
					rights.HasFlag(CastlingRights.BlackQueenSide) ? "q" : string.Empty);
	}

	/*──────────────────────────────────────────────────────────────*/
	/*  Value object (struct — C#9 compatible)                      */
	/*──────────────────────────────────────────────────────────────*/

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
		}

		public readonly CastlingRights Castling;
		public readonly int            FullmoveNumber;
		public readonly int            HalfmoveClock;
		public readonly PlayerColor    ActiveColor;
		public readonly string         EnPassant;
		public readonly string         PiecePlacement;

		/// <summary>Returns a copy with the side to move toggled.</summary>
		public FenData ToggleColor()
		{
			var newColor = ActiveColor == PlayerColor.White
				? PlayerColor.Black
				: PlayerColor.White;

			return new(
				PiecePlacement,
				newColor,
				Castling,
				EnPassant,
				HalfmoveClock,
				FullmoveNumber);
		}

		/// <summary>
		///     Expands the piece-placement field into a 64-square array
		///     (index 0 = A1, index 63 = H8). Empty squares are <c>null</c>.
		/// </summary>
		public Piece?[] ToBoardArray()
		{
			var board = new Piece?[64];
			var idx   = 56; // A8
			foreach (var rank in PiecePlacement.Split('/'))
			{
				foreach (var c in rank)
				{
					if (char.IsDigit(c))
					{
						idx += c - '0';
					}
					else
					{
						board[idx++] = new Piece(c);
					}
				}

				idx -= 16; // next lower rank
			}

			return board;
		}
	}

	/// <summary>Minimal wrapper for a piece defined by a FEN character.</summary>
	public readonly struct Piece
	{
		public Piece(char fenChar)
		{
			var isUpper = char.IsUpper(fenChar);
			Color = isUpper ? PlayerColor.White : PlayerColor.Black;

			Type = char.ToLowerInvariant(fenChar) switch
			{
				'p' => ChessPieceType.Pawn,
				'n' => ChessPieceType.Knight,
				'b' => ChessPieceType.Bishop,
				'r' => ChessPieceType.Rook,
				'q' => ChessPieceType.Queen,
				'k' => ChessPieceType.King,
				_   => ChessPieceType.None
			};
		}

		public readonly ChessPieceType Type;
		public readonly PlayerColor    Color;
	}
}
