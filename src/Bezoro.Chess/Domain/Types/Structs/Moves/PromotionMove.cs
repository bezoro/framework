using Bezoro.Chess.Domain.Abstractions;
using Bezoro.Chess.Domain.Shared.Enums;

namespace Bezoro.Chess.Domain.Types.Structs.Moves
{
	internal readonly struct CapturePromotionMove : ICapturePromotionMove
	{
		public MoveType Kind           { get; }
		public Piece    CapturedPiece  { get; }
		public Piece    MovingPiece    { get; }
		public Piece    PromotionPiece { get; }
		public Position From           { get; }
		public Position To             { get; }
	}

	internal readonly struct PromotionMove : IPromotionMove
	{
		public MoveType Kind           { get; }
		public Piece    MovingPiece    { get; }
		public Piece    PromotionPiece { get; }
		public Position From           { get; }
		public Position To             { get; }
	}
}
