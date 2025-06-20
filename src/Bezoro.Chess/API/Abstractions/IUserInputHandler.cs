using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.API.Abstractions
{
	/// <summary>
	///     Implemented by the Presenter to handle input events from the View (e.g., Unity).
	///     This defines how the View can send user actions to the Presenter.
	/// </summary>
	public interface IUserInputHandler
	{
		/// <summary>
		///     Called by the View when the user selects a piece to promote a pawn to.
		/// </summary>
		/// <param name="pieceType">The type of piece chosen for promotion (e.g., Queen, Rook).</param>
		void OnPromotionPieceSelected(PromotionType pieceType);

		/// <summary>
		///     Called by the View when the user selects a square on the chessboard.
		/// </summary>
		/// <param name="position">The board position selected by the user.</param>
		void OnSquareSelected(Position position);
	}
}
