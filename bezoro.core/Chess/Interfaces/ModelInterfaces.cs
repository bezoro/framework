using System;
using System.Collections.Generic;
using Bezoro.Core.Chess.Pieces;

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
		CastlingRights            CastlingRights { get; }
		IChessBoardSquareModel[,] Squares        { get; }
		int                       Height         { get; }
		int                       Width          { get; }
		List<IChessPieceModel>    BoardPieces    { get; }
		List<IChessPieceModel>    CapturedPieces { get; set; }
		BoardPosition? GetPosition(IChessPieceModel piece);
		bool IsEmpty(BoardPosition to);
		bool TryMovePiece(MovePieceCommand movePieceCommand);
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

	public interface IChessBoardSquareModel
	{
		BoardPosition     Position   { get; }
		bool              IsEmpty    { get; }
		bool              IsOccupied { get; }
		IChessPieceModel? Piece      { get; }
		IChessPieceModel? GetPiece();
		void ClearPiece();
		void RemovePiece(IChessPieceModel piece);
		void SetPiece(IChessPieceModel? piece);
	}

	public interface IChessPieceModel
	{
		bool        HasMoved { get; }
		PlayerColor Color    { get; }
		PlayerColor Opposite { get; }

		IEnumerable<Move> GetPseudoLegalMoves(IChessBoardModel board);
		void MarkMoved();
		void ResetMoved();
	}
}
