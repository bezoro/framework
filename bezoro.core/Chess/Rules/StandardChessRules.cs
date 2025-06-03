using System;
using System.Collections.Generic;
using Bezoro.Core.Chess;

public sealed class StandardChessRules : IGameRules
{
	public StandardChessRules(IChessBoardModel board)
	{
		_board = board;
	}

	private readonly IChessBoardModel _board;

#region Interface Implementations

	public IEnumerable<Move> FilterLegalMoves(IChessBoardModel board, IChessPieceModel piece)
	{
		foreach (var move in piece.GetPseudoLegalMoves(board))
		{
			if (IsMoveLegal(board, move))
				yield return move;
		}
	}

#endregion

	private bool IsMoveLegal(IChessBoardModel board, Move move) =>
		throw new NotImplementedException();
}
