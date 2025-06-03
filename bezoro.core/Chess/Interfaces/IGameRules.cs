using System;
using Bezoro.Core.Chess;
using Bezoro.Core.Chess.Utils;

public interface IGameRules
{
	bool IsCastleLegal(KingModel king, CastleSide side);
}

public sealed class StandardChessRules : IGameRules
{
	public StandardChessRules(IChessBoardModel board)
	{
		_board = board;
	}

	private readonly IChessBoardModel _board;

#region Interface Implementations

	public bool IsCastleLegal(KingModel king, CastleSide side)
	{
		// 1. Piece-local state
		if (king.HasMoved || king.IsInCheck) return false;

		// 2. Find the rook and make sure it hasn’t moved
		var rook = FindRookForSide(king.Color, side);
		if (rook == null || rook.HasMoved) return false;

		// 3. Ensure squares between king and rook are empty
		if (!SquaresBetweenAreEmpty(king, rook)) return false;

		// 4. Ensure none of the king’s transit squares are attacked
		if (TransitSquaresAreAttacked(king, side)) return false;

		// 5. Check global castling rights flag
		return _board.CastlingRights.HasFlag(GetFlag(king.Color, side));
	}

#endregion

	private bool SquaresBetweenAreEmpty(KingModel king, IChessPieceModel rook) =>
		throw new NotImplementedException();

	private bool TransitSquaresAreAttacked(KingModel king, CastleSide side) =>
		throw new NotImplementedException();

	private Enum GetFlag(PlayerColor kingColor, CastleSide side) =>
		throw new NotImplementedException();

	private IChessPieceModel? FindRookForSide(PlayerColor kingColor, CastleSide side) =>
		(kingColor, side) switch
		{
			(PlayerColor.White, CastleSide.KingSide) => _board.GetPieceAt("h1"),
			(PlayerColor.White, CastleSide.QueenSide) => _board.GetPieceAt("a1"),
			(PlayerColor.Black, CastleSide.KingSide) => _board.GetPieceAt("h8"),
			(PlayerColor.Black, CastleSide.QueenSide) => _board.GetPieceAt("a8"),
			(_, CastleSide.None) or (PlayerColor.None, _) => null,
			_ => throw new ArgumentOutOfRangeException(nameof(kingColor), kingColor, null)
		};
}
