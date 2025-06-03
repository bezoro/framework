using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess
{
	/// <summary>Value-type that represents a frozen board position.</summary>
	public sealed class BoardSnapshot
	{
		// ❷ Constructor visible only inside the assembly.
		internal BoardSnapshot(
			Piece?[,] pieces,
			CastlingRights rights,
			BoardPosition? enPassant,
			int halfMove,
			PlayerColor sideToMove)
		{
			Pieces          = pieces;
			CastlingRights  = rights;
			EnPassantSquare = enPassant;
			HalfMoveClock   = halfMove;
			SideToMove      = sideToMove;
		}

		public BoardPosition? EnPassantSquare { get; }
		public CastlingRights CastlingRights  { get; }
		public int            HalfMoveClock   { get; }
		public Piece?[,]      Pieces          { get; }
		public PlayerColor    SideToMove      { get; }
	}
}
