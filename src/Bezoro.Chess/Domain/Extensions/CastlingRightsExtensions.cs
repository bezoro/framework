using System;
using System.Runtime.CompilerServices;
using Bezoro.Chess.Domain.Shared.Enums;

namespace Bezoro.Chess.Domain.Extensions
{
	internal static class CastlingRightsExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Has(this CastlingRights rights, CastlingRights flag) =>
			(rights & flag) == flag;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasAny(this CastlingRights rights, CastlingRights flags) =>
			(rights & flags) != 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasKingside(this CastlingRights rights, bool isWhite) =>
			isWhite
				? (rights & CastlingRights.WhiteKingside) != 0
				: (rights & CastlingRights.BlackKingside) != 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasQueenside(this CastlingRights rights, bool isWhite) =>
			isWhite
				? (rights & CastlingRights.WhiteQueenside) != 0
				: (rights & CastlingRights.BlackQueenside) != 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsBlack(this CastlingRights rights) =>
			(rights & CastlingRights.Black) != 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsEmpty(this CastlingRights rights) =>
			rights == CastlingRights.None;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsValid(this CastlingRights rights) =>
			(rights & ~CastlingRights.All) == 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsWhite(this CastlingRights rights) =>
			(rights & CastlingRights.White) != 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static CastlingRights Add(this CastlingRights rights, CastlingRights toAdd) =>
			rights | toAdd;

		public static CastlingRights FromFen(this string fenSegment)
		{
			if (string.IsNullOrWhiteSpace(fenSegment) || fenSegment == "-")
			{
				return CastlingRights.None;
			}

			var rights = CastlingRights.None;
			foreach (char c in fenSegment)
			{
				rights |= c switch
				{
					'K' => CastlingRights.WhiteKingside,
					'Q' => CastlingRights.WhiteQueenside,
					'k' => CastlingRights.BlackKingside,
					'q' => CastlingRights.BlackQueenside,
					_ => throw new ArgumentException(
						$"Invalid castling character '{c}' in FEN segment.", nameof(fenSegment))
				};
			}

			return rights;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static CastlingRights Remove(this CastlingRights rights, CastlingRights toRemove) =>
			rights & ~toRemove;

		/// <summary>
		///     Converts the castling rights to the FEN (Forsyth-Edwards Notation) representation.
		///     Returns "-" if no castling rights are present, otherwise returns a string
		///     containing "K" for white kingside, "Q" for white queenside,
		///     "k" for black kingside, and "q" for black queenside castling rights.
		/// </summary>
		/// <param name="rights">The castling rights to convert</param>
		/// <returns>The FEN representation of the castling rights</returns>
		public static string ToFen(this CastlingRights rights) =>
			rights switch
			{
				CastlingRights.None => "-",
				_ => string.Concat(
					rights.Has(CastlingRights.WhiteKingside) ? "K" : string.Empty,
					rights.Has(CastlingRights.WhiteQueenside) ? "Q" : string.Empty,
					rights.Has(CastlingRights.BlackKingside) ? "k" : string.Empty,
					rights.Has(CastlingRights.BlackQueenside) ? "q" : string.Empty)
			};
	}
}
