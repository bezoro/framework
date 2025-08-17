using System;
using Bezoro.Chess.Domain.Shared.Enums;

namespace Bezoro.Chess.Domain.Extensions
{
	/// <summary>
	///     Quality-of-life helpers for PieceColor and PieceType.
	/// </summary>
	internal static class PieceEnumExtensions
	{
		/// <summary>
		///     Parses a FEN character, returning (Type, Color).
		///     Non-piece characters yield (None, None).
		/// </summary>
		public static (PieceType Type, PieceColor Color) FromFenChar(this char c)
		{
			PieceColor color = char.IsUpper(c) ? PieceColor.White : PieceColor.Black;
			char       lower = char.ToLowerInvariant(c);

			PieceType type = lower switch
			{
				'p' => PieceType.Pawn,
				'n' => PieceType.Knight,
				'b' => PieceType.Bishop,
				'r' => PieceType.Rook,
				'q' => PieceType.Queen,
				'k' => PieceType.King,
				_   => PieceType.None
			};

			return (type, type == PieceType.None ? PieceColor.None : color);
		}

		public static bool IsBishop(this PieceType type)  => type  == PieceType.Bishop;
		public static bool IsBlack(this PieceColor color) => color == PieceColor.Black;
		public static bool IsKing(this PieceType type)    => type  == PieceType.King;
		public static bool IsKnight(this PieceType type)  => type  == PieceType.Knight;

		public static bool IsMajorPiece(this PieceType type) =>
			type is PieceType.Rook or PieceType.Queen;

		public static bool IsMinorPiece(this PieceType type) =>
			type is PieceType.Knight or PieceType.Bishop;

		public static bool IsNone(this PieceColor color) => color == PieceColor.None;

		public static bool IsNone(this PieceType type)  => type == PieceType.None;
		public static bool IsPawn(this PieceType type)  => type == PieceType.Pawn;
		public static bool IsQueen(this PieceType type) => type == PieceType.Queen;
		public static bool IsRook(this PieceType type)  => type == PieceType.Rook;

		public static bool IsSlidingPiece(this PieceType type) =>
			type is PieceType.Bishop or PieceType.Rook or PieceType.Queen;

		public static bool IsWhite(this PieceColor color) => color == PieceColor.White;

		/// <summary>
		///     Converts a type + color to the single FEN character.
		/// </summary>
		public static char ToFENChar(this PieceType type, PieceColor color)
		{
			char c = type switch
			{
				PieceType.Pawn   => 'p',
				PieceType.Knight => 'n',
				PieceType.Bishop => 'b',
				PieceType.Rook   => 'r',
				PieceType.Queen  => 'q',
				PieceType.King   => 'k',
				_ => throw new ArgumentException(
					$"Invalid piece type {type}.", nameof(type))
			};

			return color == PieceColor.White ? char.ToUpperInvariant(c) : c;
		}

		/// <summary>
		///     PGN letter (Pawn → '\0' because PGN omits a letter for pawns).
		/// </summary>
		public static char ToPGNChar(this PieceType type) => type switch
		{
			PieceType.Knight => 'N', // PGN uses N for knight
			PieceType.Bishop => 'B',
			PieceType.Rook   => 'R',
			PieceType.Queen  => 'Q',
			PieceType.King   => 'K',
			_                => '\0'
		};

		/// <summary>
		///     Simple material scores often used for static evaluation (centipawns).
		/// </summary>
		public static int MaterialValue(this PieceType type) => type switch
		{
			PieceType.Pawn   => 100,
			PieceType.Knight => 320,
			PieceType.Bishop => 330,
			PieceType.Rook   => 500,
			PieceType.Queen  => 900,
			PieceType.King   => 20_000,
			_                => 0
		};

		/// <summary>Maps White → 0, Black → 1, else −1.</summary>
		public static int ToIndex(this PieceColor color) => color switch
		{
			PieceColor.White => 0,
			PieceColor.Black => 1,
			_                => -1
		};

		/// <summary>Returns the opposite color; <c>None</c> stays <c>None</c>.</summary>
		public static PieceColor Opposite(this PieceColor color) => color switch
		{
			PieceColor.White => PieceColor.Black,
			PieceColor.Black => PieceColor.White,
			_                => PieceColor.None
		};
	}
}
