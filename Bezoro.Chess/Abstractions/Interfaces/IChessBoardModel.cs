using System.Collections.Generic;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;

namespace Bezoro.Chess.Abstractions.Interfaces
{
	public interface IChessBoardModel
	{
		Dictionary<IChessPieceModel, BoardPosition>       PieceIndex { get; }
		IChessBoardSquareModel                            EnPassantTargetSquare { get; }
		IChessBoardSquareModel[,]                         Squares { get; }
		int                                               Height { get; }
		int                                               Width { get; }
		IReadOnlyDictionary<IChessPieceModel, List<Move>> CachedPseudoLegalMoves { get; }
		List<IChessPieceModel>                            BoardPieces { get; } // Pieces currently on the board
		BoardPosition? GetPosition(IChessPieceModel piece);
		bool IsEmpty(BoardPosition to);
		bool IsEnemy(IChessBoardSquareModel targetSquare, PlayerColor myColor);
		bool IsSquareAttacked(BoardPosition position, PlayerColor attackerColor);
		IChessBoardModel Clear();
		IEnumerable<IChessBoardSquareModel> GetStraightPath(BoardPosition from, BoardPosition to);
		IReadOnlyList<Move> GetCachedMovesFor(IChessPieceModel piece);

		List<IEnumerable<Move>> GetAllLegalMovesForSide(GameModel game, PlayerColor side);
		void CapturePieceAt(IChessPieceModel pieceToCapture, BoardPosition pos, GameModel game);
		void MovePieceTo(IChessPieceModel piece, BoardPosition from, BoardPosition to);
		void RestoreLastCapturedPiece(ChessPieceType capturedPieceType, BoardPosition capturedPosition, GameModel game);
		void SetEnPassantTargetSquare(IChessBoardSquareModel enPassantSquare);
		void SetPieceAt(IChessPieceModel pieceToMove, IChessBoardSquareModel to);
	}
}
