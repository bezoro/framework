using Bezoro.Chess.API.Shared.Enums;
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
			Data = new MoveViewModel(in m);
		}

		/// <summary>
		///     Create a <see cref="Result{T}" /> that already wraps a <see cref="MoveResult" />.
		/// </summary>
		internal static Result<MoveResult> Create(in Move move) =>
			Result<MoveResult>.Succeeded(new MoveResult(move));

		public bool IsCapture   => Data.CapturedPieceType != PieceType.None;
		public bool IsCastle    => Data.Type is MoveType.CastleKingside or MoveType.CastleQueenside;
		public bool IsPromotion => Data.PromotionPieceType != PromotionType.None;
		public bool IsQuiet     => Data.Type               == MoveType.Normal;

		public MoveViewModel Data { get; }

		/// <summary>
		///     Implicitly wrap a <see cref="MoveResult" /> into <see cref="Result{T}" />.
		///     Enables: <c>return MoveResult.FromMove(move);</c>
		/// </summary>
		public static implicit operator Result<MoveResult>(MoveResult mr) =>
			Result<MoveResult>.Succeeded(mr);

		/// <summary>
		///     Creates a failed move result with the specified reason.
		/// </summary>
		internal static Result<MoveResult> Failed(FailureReason reason) =>
			Result<MoveResult>.Failed(reason);
	}

	public struct MoveViewModel
	{
		internal MoveViewModel(in Move move)
		{
			Type               = move.Type.ToAPI();
			CapturedPieceType  = move.CapturedPiece.Type.ToAPI();
			MovingPieceType    = move.Piece.Type.ToAPI();
			From               = move.From.Coordinate;
			To                 = move.To.Coordinate;
			PromotionPieceType = move.PromotionPieceType.ToAPI();
		}

		public ChessSquareCoordinate From { get; }
		public ChessSquareCoordinate To   { get; }

		public MoveType      Type               { get; }
		public PieceType     CapturedPieceType  { get; }
		public PieceType     MovingPieceType    { get; }
		public PromotionType PromotionPieceType { get; }
	}
}
