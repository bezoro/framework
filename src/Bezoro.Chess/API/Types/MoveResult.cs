using Bezoro.Chess.Domain.Types.Structs;
using Bezoro.Core;

namespace Bezoro.Chess.API.Types
{
	/// <summary>
	///     All data from a successful move
	/// </summary>
	public readonly struct MoveResult
	{
		private MoveResult(in Move m)
		{
			Type               = m.Type;
			MovingPieceType    = m.Piece.Type;
			CapturedPieceType  = m.CapturedPiece.Type;
			From               = m.From;
			To                 = m.To;
			PromotionPieceType = m.PromotionPieceType;
		}

		/// <summary>
		///     Create a <see cref="Result{T}" /> that already wraps a <see cref="MoveResult" />.
		/// </summary>
		internal static Result<MoveResult> Create(in Move move) =>
			Result<MoveResult>.Succeeded(new MoveResult(move));

		public bool IsCapture   => CapturedPieceType != PieceType.None;
		public bool IsCastle    => Type is MoveType.CastleKingside or MoveType.CastleQueenside;
		public bool IsPromotion => PromotionPieceType != PromotionType.None;
		public bool IsQuiet     => Type               == MoveType.Normal;

		public MoveType      Type               { get; }
		public PieceType     CapturedPieceType  { get; }
		public PieceType     MovingPieceType    { get; }
		public Position      From               { get; }
		public Position      To                 { get; }
		public PromotionType PromotionPieceType { get; }

		/// <summary>
		///     Implicitly wrap a <see cref="MoveResult" /> into <see cref="Result{T}" />.
		///     Enables: <c>return MoveResult.FromMove(move);</c>
		/// </summary>
		public static implicit operator Result<MoveResult>(MoveResult mr) =>
			Result<MoveResult>.Succeeded(mr);

		internal static Result<MoveResult> Failed(FailureReason reason) =>
			Result<MoveResult>.Failed(reason);
	}
}
