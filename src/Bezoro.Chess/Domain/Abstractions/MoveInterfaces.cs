using Bezoro.Chess.Domain.Functions.Moves.Generation;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Abstractions
{
	internal interface ICaptureMove : IMove
	{
		Piece CapturedPiece { get; }
	}

	internal interface ICapturePromotionMove : ICaptureMove, IPromotionMove { }

	internal interface ICastle : IMove
	{
		CastlingSide Side     { get; }
		Position     RookFrom { get; }
		Position     RookTo   { get; }
	}

	internal interface IEnPassant : ICaptureMove { }

	internal interface IMove
	{
		MoveType Kind        { get; }
		Piece    MovingPiece { get; }
		Position From        { get; }
		Position To          { get; }
	}

	internal interface IPromotionMove : IMove
	{
		Piece PromotionPiece { get; }
	}
}
