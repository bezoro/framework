using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Bezoro.Chess.Board;

namespace Bezoro.Chess.Common.Helpers
{
	/// <summary>
	///     Utility helpers for converting between algebraic notation and <see cref="BoardPosition" />.
	///     Supports boards of arbitrary width: “a”-“z”, “aa”-“az”, … “zz”, “aaa”… (Excel-style base-26).
	/// </summary>
	public static class AlgebraicNotationUtils
	{
		private const int _MAX_FILE_TOKEN_LENGTH = 8; // 26⁸ ≈ 2e11 columns – far beyond practical use.

		/// <summary>
		///     Parse <paramref name="algebraic" /> (case-insensitive) into a position and
		///     optionally validate it against <paramref name="boardWidth" /> / <paramref name="boardHeight" />.
		/// </summary>
		/// <exception cref="ArgumentException">Malformed or out of range.</exception>
		/// <exception cref="ArgumentNullException"><paramref name="algebraic" /> is null/whitespace.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static BoardPosition FromAlgebraic(string algebraic, uint boardWidth = 8, uint boardHeight = 8)
		{
			if (string.IsNullOrWhiteSpace(algebraic))
				throw new ArgumentNullException(nameof(algebraic));

			// Split the “file” token (letters) from the “rank” token (digits).
			var i = 0;
			while (i < algebraic.Length && (algebraic[i] | 0x20) - 'a' < 26) // branchless ASCII check
				i++;

			if (i == 0 || i == algebraic.Length)
				throw new ArgumentException("Expected <letters><digits> (e.g. \"e4\").", nameof(algebraic));

			ReadOnlySpan<char> fileToken = algebraic[..i].ToLowerInvariant();
			ReadOnlySpan<char> rankToken = algebraic[i..];

			var column = FileTokenToIndex(fileToken);
			if (!int.TryParse(rankToken, NumberStyles.None, CultureInfo.InvariantCulture, out var rank1Based) ||
				rank1Based <= 0)
				throw new ArgumentException($"Invalid rank \"{rankToken.ToString()}\".", nameof(algebraic));

			var row = (uint)rank1Based - 1;

			// Optional board-size validation
			if (column >= boardWidth)
				throw new ArgumentException($"File exceeds board width ({boardWidth}).", nameof(algebraic));

			if (row >= boardHeight)
				throw new ArgumentException($"Rank exceeds board height ({boardHeight}).", nameof(algebraic));

			return new(column, row);
		}

		public static string IndexToFileToken(uint index)
		{
			Span<char> buf = stackalloc char[_MAX_FILE_TOKEN_LENGTH];
			var        pos = buf.Length;

			var value = index + 1u; // 1-based for easier math
			while (value > 0)
			{
				var rem = (value - 1) % 26;
				buf[--pos] = (char)('a' + rem);
				value      = (value     - 1) / 26;
			}

			return new(buf[pos..]);
		}

		/// <summary>Return algebraic form for an existing position.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToAlgebraic(BoardPosition pos) =>
			ToAlgebraic(pos.Column, pos.Row);

		/// <summary>Return algebraic form for zero-based indices.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToAlgebraic(uint column, uint row) =>
			$"{IndexToFileToken(column)}{row + 1}";

		private static uint FileTokenToIndex(ReadOnlySpan<char> token)
		{
			if (token.Length > _MAX_FILE_TOKEN_LENGTH)
				throw new ArgumentException("File token is too long for supported range.", nameof(token));

			long index = 0;
			foreach (var ch in token)
			{
				if (ch is < 'a' or > 'z')
					throw new ArgumentException($"Illegal file character '{ch}'.", nameof(token));

				index = checked( index * 26 + (ch - 'a' + 1) );
			}

			return checked( (uint)(index - 1) ); // zero-based
		}
	}
}
