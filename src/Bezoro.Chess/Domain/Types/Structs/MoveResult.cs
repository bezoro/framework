using System;
using System.Runtime.CompilerServices;
using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Moves;

public readonly struct MoveResult : IEquatable<MoveResult>
{
	/// <summary>A sentinel “no-op” result (e.g., for initialisation).</summary>
	public static readonly MoveResult None = default;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static MoveResult Failed(FailureReason reason) => new(reason);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static MoveResult Succeeded(in Move move) => new(move);

	public bool IsCapture
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => CapturedPieceType != PieceType.None;
	}
	public bool IsCastle
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Type is MoveType.CastleKingside or MoveType.CastleQueenside;
	}
	public bool IsPromotion
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => PromotionPieceType != PromotionType.None;
	}
	public bool IsQuiet
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Type == MoveType.Normal;
	}
	public bool IsValid
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Type != MoveType.None;
	}
	public bool Success
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Failure == FailureReason.None;
	}

	public FailureReason Failure            { get; }
	public MoveType      Type               { get; }
	public PieceType     CapturedPieceType  { get; }
	public PieceType     MovingPieceType    { get; }
	public Position      From               { get; }
	public Position      To                 { get; }
	public PromotionType PromotionPieceType { get; }

	#region Equality

	public static bool operator ==(MoveResult left, MoveResult right) => left.Equals(right);
	public static bool operator !=(MoveResult left, MoveResult right) => !left.Equals(right);

	public bool Equals(MoveResult other) =>
		Failure            == other.Failure           &&
		Type               == other.Type              &&
		CapturedPieceType  == other.CapturedPieceType &&
		MovingPieceType    == other.MovingPieceType   &&
		From               == other.From              &&
		To                 == other.To                &&
		PromotionPieceType == other.PromotionPieceType;

	public override bool Equals(object? obj) => obj is MoveResult other && Equals(other);

	public override int GetHashCode() =>
		HashCode.Combine(
			(int)Failure, (int)Type, (int)CapturedPieceType,
			(int)MovingPieceType, From, To, (int)PromotionPieceType);

	#endregion

	private MoveResult(in Move m)
	{
		Failure            = FailureReason.None;
		Type               = m.Type;
		MovingPieceType    = m.Piece.Type;
		From               = m.From;
		To                 = m.To;
		CapturedPieceType  = m.CapturedPiece.Type;
		PromotionPieceType = m.PromotionPieceType;
	}

	private MoveResult(FailureReason failure)
	{
		Failure            = failure;
		Type               = default;
		MovingPieceType    = default;
		From               = default;
		To                 = default;
		CapturedPieceType  = default;
		PromotionPieceType = default;
	}

	public enum FailureReason
	{
		None,
		InvalidMove
	}
}
