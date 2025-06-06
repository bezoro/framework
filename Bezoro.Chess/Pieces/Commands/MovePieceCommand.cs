using System;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Moves.Models;

namespace Bezoro.Chess.Pieces.Commands
{
	public sealed class MovePieceCommand : IChessCommand
	{
		/// <summary>
		///     Creates a command that moves the piece described by <paramref name="move" />.
		///     The board instance is needed only for looking up the squares – it is not
		///     modified in the constructor.
		/// </summary>
		public MovePieceCommand(Move move, IChessBoardModel board)
		{
			if (board is null)
				throw new ArgumentNullException(nameof(board));

			_move = move;

			From = board.GetSquareAt(move.From)
				   ?? throw new InvalidOperationException("Source square not found on board.");

			To = board.GetSquareAt(move.To)
				 ?? throw new InvalidOperationException("Target square not found on board.");

			PieceToMove = From.Piece
						  ?? throw new InvalidOperationException("There is no piece on the source square.");
		}

		private readonly Move _move;

		public IChessBoardSquareModel From        { get; }
		public IChessBoardSquareModel To          { get; }
		public IChessPieceModel       PieceToMove { get; }

	#region Interface Implementations

		public void Execute(IChessBoardModel board) =>
			throw new NotImplementedException();

		public void Undo(IChessBoardModel board) =>
			throw new NotImplementedException();

	#endregion
	}
}
