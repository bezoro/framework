using System;
using Bezoro.Chess.Domain.Shared.Enums;

namespace Bezoro.Chess.Domain.Types.Structs
{
	/// <summary>
	///     All legal move kinds supported by the engine.
	/// </summary>
	internal enum MoveType : byte
	{
		None,
		Normal,
		Capture,
		CastleKingside,
		CastleQueenside,
		EnPassant,
		PawnPromotion,
		PawnPromotionCapture
	}

	/// <summary>
	///     A single chess move.
	///     The struct itself exposes only the data common to every move.
	///     Payload that exists only for some kinds is kept private and accessed
	///     through zero-allocation “view” structs returned by <c>TryAs…</c>.
	/// </summary>
	internal readonly struct Move : IEquatable<Move>
	{
		// Optional payload – never exposed directly.
		private readonly Piece         _captured;
		private readonly PromotionType _promotion;

		private Move(
			Position from,
			Position to,
			Piece piece,
			MoveType type,
			Piece captured = default,
			PromotionType promotion = PromotionType.None)
		{
			From       = from;
			To         = to;
			Piece      = piece;
			Type       = type;
			_captured  = captured;
			_promotion = promotion;
		}

		public bool IsCapture   => Type is MoveType.Capture or MoveType.EnPassant or MoveType.PawnPromotionCapture;
		public bool IsCastle    => Type is MoveType.CastleKingside or MoveType.CastleQueenside;
		public bool IsEnPassant => Type == MoveType.EnPassant;
		public bool IsPromotion => Type is MoveType.PawnPromotion or MoveType.PawnPromotionCapture;
		public bool IsQuiet     => Type == MoveType.Normal;

		public bool     IsValid => Type != MoveType.None;
		public MoveType Type    { get; }
		public Piece    Piece   { get; }
		public Position From    { get; }
		public Position To      { get; }

		public static bool operator ==(Move l, Move r) => l.Equals(r);
		public static bool operator !=(Move l, Move r) => !l.Equals(r);

		#region Equality

		public bool Equals(Move other) =>
			Type == other.Type                &&
			Piece.Equals(other.Piece)         &&
			From.Equals(other.From)           &&
			To.Equals(other.To)               &&
			_captured.Equals(other._captured) &&
			_promotion == other._promotion;

		public override bool Equals(object? obj) => obj is Move m && Equals(m);

		public override int GetHashCode() =>
			HashCode.Combine((int)Type, Piece, From, To, _captured, (int)_promotion);

		#endregion

		public static Move Capture(Position from, Position to, Piece piece, Piece captured) =>
			new(from, to, piece, MoveType.Capture, captured);

		public static Move CastleKingside(Position from, Position to, Piece king) =>
			new(from, to, king, MoveType.CastleKingside);

		public static Move CastleQueenside(Position from, Position to, Piece king) =>
			new(from, to, king, MoveType.CastleQueenside);

		public static Move EnPassant(Position from, Position to, Piece pawn, Piece capturedPawn) =>
			new(from, to, pawn, MoveType.EnPassant, capturedPawn);

		public static Move Normal(Position from, Position to, Piece piece) =>
			new(from, to, piece, MoveType.Normal);

		public static Move Promotion(Position from, Position to, Piece pawn, PromotionType promotion) =>
			new(from, to, pawn, MoveType.PawnPromotion, promotion: promotion);

		public static Move PromotionCapture(
			Position from,
			Position to,
			Piece pawn,
			Piece captured,
			PromotionType promotion) =>
			new(from, to, pawn, MoveType.PawnPromotionCapture, captured, promotion);

		public override string ToString() => $"{Type}: {From}->{To}";

		public bool TryAsCapture(out CaptureView view)
		{
			if (IsCapture)
			{
				view = new CaptureView(this);
				return true;
			}

			view = default;
			return false;
		}

		public bool TryAsCastle(out CastleView view)
		{
			if (IsCastle)
			{
				view = new CastleView(this);
				return true;
			}

			view = default;
			return false;
		}

		public bool TryAsPromotion(out PromotionView view)
		{
			if (IsPromotion)
			{
				view = new PromotionView(this);
				return true;
			}

			view = default;
			return false;
		}

		public CaptureView?   AsCapture()   => IsCapture ? new CaptureView(this) : null;
		public CastleView?    AsCastle()    => IsCastle ? new CastleView(this) : null;
		public PromotionView? AsPromotion() => IsPromotion ? new PromotionView(this) : null;

		public readonly struct CaptureView
		{
			private readonly Move _m;

			internal CaptureView(in Move m)
			{
				_m = m;
			}

			public bool  IsEnPassant   => _m.Type == MoveType.EnPassant;
			public bool  IsPromotion   => _m.Type == MoveType.PawnPromotionCapture;
			public Piece CapturedPiece => _m._captured;
			public Piece Piece         => _m.Piece;

			public Position From => _m.From;
			public Position To   => _m.To;
		}

		public readonly struct CastleView
		{
			private readonly Move _m;

			internal CastleView(in Move m)
			{
				_m = m;
			}

			public bool  IsKingside  => _m.Type == MoveType.CastleKingside;
			public bool  IsQueenside => _m.Type == MoveType.CastleQueenside;
			public Piece King        => _m.Piece;

			public Position From => _m.From;
			public Position To   => _m.To;
		}

		public readonly struct PromotionView
		{
			private readonly Move _m;

			internal PromotionView(in Move m)
			{
				_m = m;
			}

			public bool  IsCapture => _m.Type == MoveType.PawnPromotionCapture;
			public Piece Pawn      => _m.Piece;

			public Position      From               => _m.From;
			public Position      To                 => _m.To;
			public PromotionType PromotionPieceType => _m._promotion;
		}
	}
}
