using Bezoro.Chess.Board;

namespace Bezoro.Chess.Abstractions.Interfaces
{
	public interface IChessBoardModel
	{
		IChessBoardSquareModel?   EnPassantSquare { get; }
		IChessBoardSquareModel[,] Squares         { get; }
		uint                      Height          { get; }
		uint                      Width           { get; }

		/// <summary>
		///     Clears the board of pieces
		/// </summary>
		IChessBoardModel ClearPieces();

		/// <summary>
		///     Resets the entire board state to the starting setup.
		/// </summary>
		IChessBoardModel Reset();

		/// <summary>
		///     Moves the piece from the given position to the given position.
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		void MovePiece(BoardPosition from, BoardPosition to);

		/// <summary>
		///     Sets the en passant square.
		/// </summary>
		/// <param name="enPassantSquare"></param>
		void SetEnPassantSquare(IChessBoardSquareModel? enPassantSquare);

		void SetPieceAt(BoardPosition position, IChessPieceModel? piece);
	}
}
