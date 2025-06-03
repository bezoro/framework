using System;
using System.Linq;
using Bezoro.Core.Chess;
using Bezoro.Core.Chess.Utils;

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
		if (king == null) throw new ArgumentNullException(nameof(king));
		if (side == CastleSide.None) return false;

		// 1. Piece-local state
		if (king.HasMoved || king.IsInCheck) return false;

		// 2. Locate rook & verify state
		var rook = FindRookForSide(king.Color, side);
		if (rook == null || rook.HasMoved) return false;

		// 3. Squares between king and rook must be empty
		if (!SquaresBetweenAreEmpty(king, rook)) return false;

		// 4. Transit squares must not be attacked
		if (TransitSquaresAreAttacked(king, side)) return false;

		// 5. Global rights flag
		return _board.CastlingRights.HasFlag(GetFlag(king.Color, side));
	}

#endregion

	/* --------------------------------------------------------------------
	   Helpers
	   ------------------------------------------------------------------*/

	/// <summary>
	///     True if no occupied squares lie strictly between <paramref name="king" />
	///     and <paramref name="rook" /> (exclusive).
	/// </summary>
	private bool SquaresBetweenAreEmpty(KingModel king, IChessPieceModel rook)
	{
		var start = _board.GetPosition(king)!.Value;
		var end   = _board.GetPosition(rook)!.Value;

		// Determining direction once avoids extra branching later
		var dx = Math.Sign(end.File - start.File);
		var dy = Math.Sign(end.Rank - start.Rank);

		// Collect the sliding ray and stop *before* the rook’s square
		var intermediates = _board
							.WalkRay(start, dx, dy)
							.TakeWhile(sq => !sq.Equals(end));

		return intermediates.All(sq => sq.Piece == null);
	}

	/// <summary>
	///     Returns <c>true</c> if any of the king’s through-squares (the two
	///     squares the king will touch while castling) are attacked.
	/// </summary>
	private bool TransitSquaresAreAttacked(KingModel king, CastleSide side)
	{
		var fileStep = side == CastleSide.KingSide ? 1 : -1;
		var kingPos  = _board.GetPosition(king).Value;
		// f-file & g-file (or d-file & c-file for queen side)
		var transitFiles = new[]
		{
			kingPos.File + fileStep,
			kingPos.File + 2 * fileStep
		};

		foreach (var file in transitFiles)
		{
			if (!_board.IsInside(file, kingPos.Rank)) return true; // should not occur
			var sq = _board.Squares[file, kingPos.Rank];
			if (_board.IsSquareAttacked(sq, king.Opposite)) return true;
		}

		return false;
	}

	/// <summary>
	///     Convenience mapper translating <see cref="PlayerColor" />/<see cref="CastleSide" />
	///     to the appropriate <see cref="CastlingRights" /> flag.
	/// </summary>
	private static CastlingRights GetFlag(PlayerColor color, CastleSide side) =>
		(color, side) switch
		{
			(PlayerColor.White, CastleSide.KingSide)  => CastlingRights.WhiteKingSide,
			(PlayerColor.White, CastleSide.QueenSide) => CastlingRights.WhiteQueenSide,
			(PlayerColor.Black, CastleSide.KingSide)  => CastlingRights.BlackKingSide,
			(PlayerColor.Black, CastleSide.QueenSide) => CastlingRights.BlackQueenSide,
			_                                         => CastlingRights.None
		};

	private IChessPieceModel? FindRookForSide(PlayerColor kingColor, CastleSide side) =>
		(kingColor, side) switch
		{
			(PlayerColor.White, CastleSide.KingSide)  => _board.GetPieceAt("h1"),
			(PlayerColor.White, CastleSide.QueenSide) => _board.GetPieceAt("a1"),
			(PlayerColor.Black, CastleSide.KingSide)  => _board.GetPieceAt("h8"),
			(PlayerColor.Black, CastleSide.QueenSide) => _board.GetPieceAt("a8"),
			_                                         => null
		};
}
