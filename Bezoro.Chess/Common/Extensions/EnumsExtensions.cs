using System;
using System.Runtime.CompilerServices;
using System.Text;
using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Common.Extensions
{
	/// <summary>
	///     Helper methods that add frequently-needed behaviour to the chess core enums.
	/// </summary>
	public static class EnumsExtensions
	{
		private static readonly char[] BlackPieceChars = { 'p', 'n', 'b', 'r', 'q', 'k' };

		/* Index: 0 = Pawn … 5 = King  (ChessPieceType.None = -1) */
		private static readonly char[] WhitePieceChars = { 'P', 'N', 'B', 'R', 'Q', 'K' };

		/// <summary>
		///     Converts a FEN piece letter into the corresponding tuple of piece-type and colour.
		/// </summary>
		/// <exception cref="ArgumentException">Thrown when <paramref name="c" /> is not a legal FEN piece character.</exception>
		public static (ChessPieceType Piece, PlayerColor Color) FromFenChar(this char c) =>
			c switch
			{
				'P' => (ChessPieceType.Pawn, PlayerColor.White),
				'N' => (ChessPieceType.Knight, PlayerColor.White),
				'B' => (ChessPieceType.Bishop, PlayerColor.White),
				'R' => (ChessPieceType.Rook, PlayerColor.White),
				'Q' => (ChessPieceType.Queen, PlayerColor.White),
				'K' => (ChessPieceType.King, PlayerColor.White),
				'p' => (ChessPieceType.Pawn, PlayerColor.Black),
				'n' => (ChessPieceType.Knight, PlayerColor.Black),
				'b' => (ChessPieceType.Bishop, PlayerColor.Black),
				'r' => (ChessPieceType.Rook, PlayerColor.Black),
				'q' => (ChessPieceType.Queen, PlayerColor.Black),
				'k' => (ChessPieceType.King, PlayerColor.Black),
				_   => throw new ArgumentException($"Invalid FEN piece character: {c}", nameof(c))
			};

		/// <summary>True if <paramref name="color" /> equals <see cref="PlayerColor.Black" />.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsBlack(this PlayerColor color) => color == PlayerColor.Black;

		/// <summary>True if <paramref name="color" /> equals <see cref="PlayerColor.White" />.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsWhite(this PlayerColor color) => color == PlayerColor.White;

		/// <summary>
		///     Parses a FEN castling segment back into <see cref="CastlingRights" />.
		///     Any unknown character will result in an <see cref="ArgumentException" />.
		/// </summary>
		public static CastlingRights CastlingRightsFromFen(this string fenSegment)
		{
			if (string.IsNullOrEmpty(fenSegment) || fenSegment == "-") return CastlingRights.None;

			var rights = CastlingRights.None;
			foreach (var ch in fenSegment)
			{
				rights |= ch switch
				{
					'K' => CastlingRights.WhiteKingSide,
					'Q' => CastlingRights.WhiteQueenSide,
					'k' => CastlingRights.BlackKingSide,
					'q' => CastlingRights.BlackQueenSide,
					_ => throw new ArgumentException(
						$"Invalid castling character in FEN string: '{ch}'.", nameof(fenSegment))
				};
			}

			return rights;
		}

		/// <summary>
		///     Returns the FEN character for a promotion target piece.
		/// </summary>
		/// <remarks>
		///     <see cref="PromotionPieceType" /> has the same ordering as <see cref="ChessPieceType" /> for the promotable pieces,
		///     therefore a simple cast allows reuse of <see cref="ToFenChar" />.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char FenChar(this PromotionPieceType promo, PlayerColor color) =>
			((ChessPieceType)promo).ToFenChar(color);

		/// <summary>
		///     Returns the single-letter FEN representation for the given chess piece <paramref name="pieceType" /> and colour.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		///     Thrown when <paramref name="pieceType" /> is outside the defined range or is <see cref="ChessPieceType.None" />.
		/// </exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char ToFenChar(this ChessPieceType pieceType, PlayerColor color)
		{
			if ((int)pieceType is < 0 or > 5)
				throw new ArgumentOutOfRangeException(nameof(pieceType), pieceType, "Unknown piece type.");

			return color switch
			{
				PlayerColor.White => WhitePieceChars[(int)pieceType],
				PlayerColor.Black => BlackPieceChars[(int)pieceType],
				_                 => throw new ArgumentException("Cannot map None colour to FEN char.", nameof(color))
			};
		}

		/// <summary>Returns the opposite colour, or <c>PlayerColor.None</c> when unknown.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PlayerColor Opposite(this PlayerColor color) =>
			color switch
			{
				PlayerColor.White => PlayerColor.Black,
				PlayerColor.Black => PlayerColor.White,
				_                 => PlayerColor.None
			};

		/// <summary>
		///     Serialises castling rights to their FEN substring (<c>"KQkq"</c>, <c>"Kq"</c>, <c>"-"</c>, …).
		/// </summary>
		public static string ToFenString(this CastlingRights rights)
		{
			if (rights == CastlingRights.None) return "-";

			var sb = new StringBuilder(4);
			if (rights.HasFlag(CastlingRights.WhiteKingSide)) sb.Append('K');
			if (rights.HasFlag(CastlingRights.WhiteQueenSide)) sb.Append('Q');
			if (rights.HasFlag(CastlingRights.BlackKingSide)) sb.Append('k');
			if (rights.HasFlag(CastlingRights.BlackQueenSide)) sb.Append('q');

			return sb.Length == 0 ? "-" : sb.ToString();
		}
	}
}
