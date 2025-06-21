using System;
using System.Runtime.CompilerServices;

namespace Bezoro.Chess.Domain.Types.Structs
{
	/// <summary>
	///     Immutable collection of the 12 piece-specific bitboards used by the engine.
	/// </summary>
	internal readonly struct BoardBitboards : IEquatable<BoardBitboards>
	{
		// ── Black ────────────────────────────────────────────────────────────
		public readonly ulong BlackPawns, BlackKnights, BlackBishops, BlackRooks, BlackQueens, BlackKing;
		// ── White ────────────────────────────────────────────────────────────
		public readonly ulong WhitePawns, WhiteKnights, WhiteBishops, WhiteRooks, WhiteQueens, WhiteKing;

		/// <summary>Main constructor – uses colour-specific value objects.</summary>
		public BoardBitboards(ColorBitboards white, ColorBitboards black)
		{
			WhitePawns   = white.Pawns;
			WhiteKnights = white.Knights;
			WhiteBishops = white.Bishops;
			WhiteRooks   = white.Rooks;
			WhiteQueens  = white.Queens;
			WhiteKing    = white.King;

			BlackPawns   = black.Pawns;
			BlackKnights = black.Knights;
			BlackBishops = black.Bishops;
			BlackRooks   = black.Rooks;
			BlackQueens  = black.Queens;
			BlackKing    = black.King;
		}

		public ulong BlackPieces => BlackPawns | BlackKnights | BlackBishops | BlackRooks | BlackQueens | BlackKing;
		public ulong Empty       => ~Occupied;
		public ulong Occupied    => WhitePieces | BlackPieces;
		public ulong WhitePieces => WhitePawns  | WhiteKnights | WhiteBishops | WhiteRooks | WhiteQueens | WhiteKing;

		public static bool operator ==(BoardBitboards left, BoardBitboards right) => left.Equals(right);
		public static bool operator !=(BoardBitboards left, BoardBitboards right) => !left.Equals(right);

		#region Equality

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(BoardBitboards other)
		{
			ulong diff = BlackPawns ^ other.BlackPawns;
			diff |= BlackKnights ^ other.BlackKnights;
			diff |= BlackBishops ^ other.BlackBishops;
			diff |= BlackRooks   ^ other.BlackRooks;
			diff |= BlackQueens  ^ other.BlackQueens;
			diff |= BlackKing    ^ other.BlackKing;
			diff |= WhitePawns   ^ other.WhitePawns;
			diff |= WhiteKnights ^ other.WhiteKnights;
			diff |= WhiteBishops ^ other.WhiteBishops;
			diff |= WhiteRooks   ^ other.WhiteRooks;
			diff |= WhiteQueens  ^ other.WhiteQueens;
			diff |= WhiteKing    ^ other.WhiteKing;

			return diff == 0;
		}

		public override bool Equals(object? obj) => obj is BoardBitboards other && Equals(other);

		public override int GetHashCode()
		{
			HashCode hash = new();
			hash.Add(BlackPawns);
			hash.Add(BlackKnights);
			hash.Add(BlackBishops);
			hash.Add(BlackRooks);
			hash.Add(BlackQueens);
			hash.Add(BlackKing);
			hash.Add(WhitePawns);
			hash.Add(WhiteKnights);
			hash.Add(WhiteBishops);
			hash.Add(WhiteRooks);
			hash.Add(WhiteQueens);
			hash.Add(WhiteKing);
			return hash.ToHashCode();
		}

		#endregion
	}
}
