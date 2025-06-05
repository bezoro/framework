using System;
using System.Runtime.CompilerServices;
using System.Text;

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
		public const string EMPTY_FEN = "8/8/8/8/8/8/8/8 w - - 0 1";
		public const string START_FEN = START_PIECES + " w KQkq - 0 1";
		public const string START_PIECES =
			"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR";

		public static FenData EmptyBoard  { get; } = Parse(EMPTY_FEN);
		public static FenData StartBoard  { get; } = Parse(START_FEN);
		public static string  StartPieces => START_PIECES;

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

			// Split the FEN string by spaces, removing empty entries.
			var parts = fen.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

			if (parts.Length is 0 or > 6)
			{
				error = "FEN string must contain between 1 and 6 space-separated fields.";
				return false;
			}

			// Field 1: Piece Placement (Mandatory)
			var piecePlacement = parts[0];
			if (!FenValidators.IsValidPiecePlacement(piecePlacement)) // Assuming FenValidators exists
			{
				error = "Invalid piece-placement section.";
				return false;
			}

			// Field 2: Active Color (Default: 'w')
			var activeColorStr = parts.Length > 1 ? parts[1] : "w";
			if (!FenExtensions.TryParseColor(activeColorStr, out var color))
			{
				error = "Active color must be 'w' or 'b'.";
				return false;
			}

			// Field 3: Castling Availability (Default: '-')
			var castlingStr = parts.Length > 2 ? parts[2] : "-";
			if (!FenExtensions.TryParseCastling(castlingStr, out var rights))
			{
				error = "Malformed castling section.";
				return false;
			}

			// Field 4: En Passant Target Square (Default: '-')
			var enPassantStr = parts.Length > 3 ? parts[3] : "-";
			// Assuming FenValidators.TryParseEnPassant validates and outputs the string representation (e.g. "e3" or "-")
			if (!FenValidators.TryParseEnPassant(enPassantStr, out var epSquareString))
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

		/*──────────────────────────────────────────────────────────*/
		/*  Public API                                              */
		/*──────────────────────────────────────────────────────────*/

		/// <exception cref="ArgumentException">Malformed FEN.</exception>
		public static FenData Parse(string fen)
		{
			if (!TryParse(fen, out var data, out var err))
				throw new ArgumentException(err ?? "Invalid FEN string.", nameof(fen));

			return data;
		}

		public static string Format(in FenData fen) =>
			string.Concat(
				fen.PiecePlacement, " ",
				fen.ActiveColor.ToFenChar(), " ",
				fen.Castling.ToFenString(), " ",
				fen.EnPassant, " ", // FenData.EnPassant should be the string representation
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

		public static bool TryParseColor(string token, out PlayerColor color)
		{
			color = PlayerColor.None; // Default to an invalid state
			if (token.Length == 1)
			{
				switch (token[0])
				{
					case 'w':
						color = PlayerColor.White;
						return true;
					case 'b':
						color = PlayerColor.Black;
						return true;
				}
			}

			return false;
		}

		// The TrySplitIntoSixTokens method is no longer needed and can be removed.
		// public static bool TrySplitIntoSixTokens(this string src, Span<string> dest) { ... }

		/*——— PlayerColor ———*/
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char ToFenChar(this PlayerColor color) =>
			color == PlayerColor.White ? 'w' : 'b';

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

	// Assuming FenValidators.TryParseEnPassant and other parts of FenData, Piece, etc. remain as they were,
	// and FenValidators.IsValidPiecePlacement exists.
	// If FenValidators is not defined elsewhere, you'll need to implement it.
	// For example:
	// public static class FenValidators {
	// public static bool IsValidPiecePlacement(string piecePlacement) { /* ... */ return true; }
	// public static bool TryParseEnPassant(string fenEp, out string epSquare) {
	// epSquare = fenEp; // Simplistic, proper validation needed
	// if (fenEp == "-") return true;
	// // Basic check: a-h followed by 3 or 6 for white/black en passant
	// if (fenEp.Length == 2 && fenEp[0] >= 'a' && fenEp[0] <= 'h' && (fenEp[1] == '3' || fenEp[1] == '6')) return true;
	// return false;
	// }
	// }

	/*──────────────────────────────────────────────────────────────*/
	/*  Value object (struct — C#9 compatible)                      */
	/*──────────────────────────────────────────────────────────────*/

	public readonly struct FenData
	{
		public FenData(
			string piecePlacement,
			PlayerColor activeColor,
			CastlingRights castling,
			string enPassant, // This should be the string like "e3" or "-"
			int halfmoveClock,
			int fullmoveNumber)
		{
			PiecePlacement = piecePlacement;
			ActiveColor    = activeColor;
			Castling       = castling;
			EnPassant      = enPassant; // Store the string directly
			HalfmoveClock  = halfmoveClock;
			FullmoveNumber = fullmoveNumber;
		}

		public readonly CastlingRights Castling;
		public readonly int            FullmoveNumber;
		public readonly int            HalfmoveClock;
		public readonly PlayerColor    ActiveColor;
		public readonly string         EnPassant; // String representation like "e3" or "-"
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
