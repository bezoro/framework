using System;
using System.Collections.Generic;
using Bezoro.Chess.Chess.Board;
using Bezoro.Chess.Chess.Common.Enums;
using Bezoro.Chess.Chess.Game.Models;
using Bezoro.Chess.Chess.Moves.Models;

namespace Bezoro.Chess.Chess.Abstractions.Interfaces
{
	public interface IChessBoardModel
	{
		Dictionary<IChessPieceModel, BoardPosition> PieceIndex { get; }
		IChessBoardSquareModel[,]                   Squares    { get; }
		int                                         Height     { get; }
		int                                         Width      { get; }
		/// <summary>
		///     Read-only view of the last computed pseudo-legal moves for all pieces.
		/// </summary>
		IReadOnlyDictionary<IChessPieceModel, List<Move>> CachedPseudoLegalMoves { get; }
		List<IChessPieceModel> BoardPieces { get; } // Pieces currently on the board
		BoardPosition? GetPosition(IChessPieceModel piece);
		bool IsEmpty(BoardPosition to);
		bool IsEnemy(IChessBoardSquareModel targetSquare, PlayerColor myColor);
		bool IsSquareAttacked(BoardPosition position, PlayerColor attackerColor);
		IEnumerable<IChessBoardSquareModel> GetStraightPath(BoardPosition from, BoardPosition to);

		/// <summary>
		///     Retrieves the cached moves for a specific piece.
		///     Returns an empty collection if the piece is unknown or no cache is present.
		/// </summary>
		IReadOnlyList<Move> GetCachedMovesFor(IChessPieceModel piece);

		void Clear();

		void MovePiece(IChessPieceModel piece, string fromAlgebraic, string toAlgebraic);
		void MovePiece(IChessPieceModel piece, BoardPosition from, BoardPosition to);

		/// <summary>
		///     Rebuilds the cache that maps every on-board piece to the full set of its
		///     current pseudo-legal moves.
		///     This cache can later be consulted (e.g. when verifying that the king is not
		///     placed in check by a prospective move).
		/// </summary>
		/// <param name="game">
		///     The <see cref="GameModel" /> instance providing the necessary context to each
		///     piece’s <see cref="IChessPieceModel.GetPseudoLegalMoves" /> implementation.
		/// </param>
		/// <exception cref="ArgumentNullException">
		///     Thrown if <paramref name="game" /> is <c>null</c>.
		/// </exception>
		void RefreshPseudoLegalMoveCache(GameModel game);

		void SetPieceAt(IChessPieceModel pieceToMove, IChessBoardSquareModel to);
	}
}
