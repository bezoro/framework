using System;
using System.Collections.Generic;

namespace Bezoro.Core.Chess.Interfaces
{
	public interface IChessBoardModel
	{
		/// <summary>
		///     Returns a read-only picture of the current position.
		///     The object is created once per position and reused until the
		///     board changes again.
		/// </summary>
		BoardSnapshot Snapshot { get; }
		IChessBoardSquareModel[,] Squares     { get; }
		int                       Height      { get; }
		int                       Width       { get; }
		List<IChessPieceModel>    BoardPieces { get; }
		BoardPosition? GetPosition(IChessPieceModel piece);
		bool IsEmpty(BoardPosition to);
		IChessBoardSquareModel GetSquare(BoardPosition position);

		/// <summary>
		///     Lists every square in a straight path between <paramref name="from" />
		///     and <paramref name="to" />, excluding the endpoints.
		///     Ideal for validating that all squares between the king and rook are empty
		///     when evaluating castling rights.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		///     Thrown when the two squares are not on the same rank or file.
		/// </exception>
		IEnumerable<IChessBoardSquareModel> GetStraightPath(
			BoardPosition from,
			BoardPosition to);

		void MovePiece(
			IChessPieceModel piece,
			BoardPosition from,
			BoardPosition to);

		void MovePiece(
			IChessPieceModel piece,
			string fromAlgebraic,
			string toAlgebraic);

		void SetPieceAt(IChessPieceModel pieceToMove, IChessBoardSquareModel to);
	}
}
