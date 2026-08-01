using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.Internal;

internal static partial class LocalPositionRules
{
	private static readonly (int File, int Rank)[] DiagonalDirections =
	[
		(-1, -1), (-1, 1), (1, -1), (1, 1)
	];

	private static readonly (int File, int Rank)[] OrthogonalDirections =
	[
		(-1, 0), (1, 0), (0, -1), (0, 1)
	];

	private static readonly (int File, int Rank)[] QueenDirections =
	[
		(-1, -1), (-1, 0), (-1, 1),
		(0, -1), (0, 1),
		(1, -1), (1, 0), (1, 1)
	];

	private static readonly (int File, int Rank)[] KnightOffsets =
	[
		(-2, -1), (-2, 1), (-1, -2), (-1, 2),
		(1, -2), (1, 2), (2, -1), (2, 1)
	];

	private static readonly (int File, int Rank)[] KingOffsets =
	[
		(-1, -1), (-1, 0), (-1, 1),
		(0, -1), (0, 1),
		(1, -1), (1, 0), (1, 1)
	];

	private static ImmutableArray<string> GenerateLegalMoves(LocalPositionState state)
	{
		var moves = ImmutableArray.CreateBuilder<string>(64);
		TraverseLegalMoves(state, moves);
		return moves.ToImmutable();
	}

	private static bool HasAnyLegalMove(LocalPositionState state) => TraverseLegalMoves(state, null);

	private static bool TraverseLegalMoves(
		LocalPositionState             state,
		ImmutableArray<string>.Builder? moves)
	{
		for (var fromIndex = 0; fromIndex < state.Board.Length; fromIndex++)
		{
			char piece = state.Board[fromIndex];
			if (piece == '\0' || GetPieceColor(piece) != state.ActiveColor)
				continue;

			bool found = char.ToLowerInvariant(piece) switch
			{
				'p' => AddLegalPawnMoves(state, fromIndex, moves),
				'n' => AddLegalKnightMoves(state, fromIndex, moves),
				'b' => AddLegalSlidingMoves(state, fromIndex, DiagonalDirections, moves),
				'r' => AddLegalSlidingMoves(state, fromIndex, OrthogonalDirections, moves),
				'q' => AddLegalSlidingMoves(state, fromIndex, QueenDirections, moves),
				'k' => AddLegalKingMoves(state, fromIndex, moves),
				_ => false
			};

			if (found)
				return true;
		}

		return false;
	}

	private static bool AddLegalPawnMoves(
		LocalPositionState             state,
		int                            fromIndex,
		ImmutableArray<string>.Builder? moves)
	{
		int file = GetFile(fromIndex);
		int rank = GetRank(fromIndex);
		int forward = state.ActiveColor == 'w' ? 1 : -1;
		int nextRank = rank + forward;
		if (!IsOnBoard(file, nextRank))
			return false;

		int promotionRank = state.ActiveColor == 'w' ? 7 : 0;
		int oneForwardIndex = GetIndex(file, nextRank);
		if (state.Board[oneForwardIndex] == '\0')
		{
			if (AddPawnMoveIfLegal(state, fromIndex, oneForwardIndex, nextRank == promotionRank, false, moves))
				return true;

			int startRank = state.ActiveColor == 'w' ? 1 : 6;
			if (rank == startRank)
			{
				int twoForwardIndex = GetIndex(file, rank + (2 * forward));
				if (state.Board[twoForwardIndex] == '\0' &&
					IsLegalMove(state, fromIndex, twoForwardIndex) &&
					AddMove(moves, fromIndex, twoForwardIndex))
					return true;
			}
		}

		for (int fileDelta = -1; fileDelta <= 1; fileDelta += 2)
		{
			int targetFile = file + fileDelta;
			if (!IsOnBoard(targetFile, nextRank))
				continue;

			int targetIndex = GetIndex(targetFile, nextRank);
			bool isEnPassant = string.Equals(
				state.EnPassantTarget,
				FormatSquare(targetIndex),
				StringComparison.Ordinal
			);
			if (!isEnPassant && !IsEnemyPiece(state.Board[targetIndex], state.ActiveColor))
				continue;

			if (AddPawnMoveIfLegal(
					state,
					fromIndex,
					targetIndex,
					nextRank == promotionRank,
					isEnPassant,
					moves))
				return true;
		}

		return false;
	}

	private static bool AddPawnMoveIfLegal(
		LocalPositionState             state,
		int                            fromIndex,
		int                            toIndex,
		bool                           isPromotion,
		bool                           isEnPassant,
		ImmutableArray<string>.Builder? moves)
	{
		if (!isPromotion)
			return IsLegalMove(state, fromIndex, toIndex, isEnPassant) && AddMove(moves, fromIndex, toIndex);

		if (moves is null)
			return IsLegalMove(state, fromIndex, toIndex, isEnPassant, promotionPiece: 'q');

		foreach (char promotionPiece in "qrbn")
		{
			if (IsLegalMove(state, fromIndex, toIndex, isEnPassant, promotionPiece: promotionPiece))
				AddMove(moves, fromIndex, toIndex, promotionPiece);
		}

		return false;
	}

	private static bool AddLegalKnightMoves(
		LocalPositionState             state,
		int                            fromIndex,
		ImmutableArray<string>.Builder? moves)
	{
		int file = GetFile(fromIndex);
		int rank = GetRank(fromIndex);
		foreach ((int fileDelta, int rankDelta) in KnightOffsets)
		{
			int targetFile = file + fileDelta;
			int targetRank = rank + rankDelta;
			if (!IsOnBoard(targetFile, targetRank))
				continue;

			int targetIndex = GetIndex(targetFile, targetRank);
			if (!IsFriendlyPiece(state.Board[targetIndex], state.ActiveColor) &&
				IsLegalMove(state, fromIndex, targetIndex) &&
				AddMove(moves, fromIndex, targetIndex))
				return true;
		}

		return false;
	}

	private static bool AddLegalSlidingMoves(
		LocalPositionState             state,
		int                            fromIndex,
		(int File, int Rank)[]         directions,
		ImmutableArray<string>.Builder? moves)
	{
		int file = GetFile(fromIndex);
		int rank = GetRank(fromIndex);
		foreach ((int fileDelta, int rankDelta) in directions)
		{
			int targetFile = file + fileDelta;
			int targetRank = rank + rankDelta;
			while (IsOnBoard(targetFile, targetRank))
			{
				int targetIndex = GetIndex(targetFile, targetRank);
				char targetPiece = state.Board[targetIndex];
				if (IsFriendlyPiece(targetPiece, state.ActiveColor))
					break;

				if (IsLegalMove(state, fromIndex, targetIndex) && AddMove(moves, fromIndex, targetIndex))
					return true;

				if (targetPiece != '\0')
					break;

				targetFile += fileDelta;
				targetRank += rankDelta;
			}
		}

		return false;
	}

	private static bool AddLegalKingMoves(
		LocalPositionState             state,
		int                            fromIndex,
		ImmutableArray<string>.Builder? moves)
	{
		int file = GetFile(fromIndex);
		int rank = GetRank(fromIndex);
		foreach ((int fileDelta, int rankDelta) in KingOffsets)
		{
			int targetFile = file + fileDelta;
			int targetRank = rank + rankDelta;
			if (!IsOnBoard(targetFile, targetRank))
				continue;

			int targetIndex = GetIndex(targetFile, targetRank);
			if (!IsFriendlyPiece(state.Board[targetIndex], state.ActiveColor) &&
				IsLegalMove(state, fromIndex, targetIndex) &&
				AddMove(moves, fromIndex, targetIndex))
				return true;
		}

		if (CanCastle(state, true) && AddMove(moves, fromIndex, GetIndex(6, rank)))
			return true;

		return CanCastle(state, false) && AddMove(moves, fromIndex, GetIndex(2, rank));
	}

	private static bool AddMove(
		ImmutableArray<string>.Builder? moves,
		int                            fromIndex,
		int                            toIndex,
		char                           promotionPiece = '\0')
	{
		if (moves is null)
			return true;

		moves.Add(FormatMove(fromIndex, toIndex, promotionPiece));
		return false;
	}

	private static bool CanCastle(LocalPositionState state, bool kingside)
	{
		char king = state.ActiveColor == 'w' ? 'K' : 'k';
		int kingIndex = Array.IndexOf(state.Board, king);
		if (kingIndex < 0 || IsKingInCheck(state.Board, state.ActiveColor))
			return false;

		string requiredRight = state.ActiveColor == 'w'
			? kingside ? "K" : "Q"
			: kingside ? "k" : "q";
		if (!state.CastlingRights.Contains(requiredRight, StringComparison.Ordinal))
			return false;

		int rank = state.ActiveColor == 'w' ? 0 : 7;
		if (kingIndex != GetIndex(4, rank))
			return false;

		if (kingside)
		{
			if (state.Board[GetIndex(5, rank)] != '\0' || state.Board[GetIndex(6, rank)] != '\0' ||
				state.Board[GetIndex(7, rank)] != (state.ActiveColor == 'w' ? 'R' : 'r') ||
				IsSquareAttacked(state.Board, GetIndex(5, rank), Opposite(state.ActiveColor)) ||
				IsSquareAttacked(state.Board, GetIndex(6, rank), Opposite(state.ActiveColor)))
				return false;

			return IsLegalMove(state, kingIndex, GetIndex(6, rank), isKingsideCastling: true);
		}

		if (state.Board[GetIndex(1, rank)] != '\0' ||
			state.Board[GetIndex(2, rank)] != '\0' ||
			state.Board[GetIndex(3, rank)] != '\0' ||
			state.Board[GetIndex(0, rank)] != (state.ActiveColor == 'w' ? 'R' : 'r') ||
			IsSquareAttacked(state.Board, GetIndex(3, rank), Opposite(state.ActiveColor)) ||
			IsSquareAttacked(state.Board, GetIndex(2, rank), Opposite(state.ActiveColor)))
			return false;

		return IsLegalMove(state, kingIndex, GetIndex(2, rank), isQueensideCastling: true);
	}

	private static bool IsLegalMove(
		LocalPositionState state,
		int                fromIndex,
		int                toIndex,
		bool               isEnPassant         = false,
		bool               isKingsideCastling  = false,
		bool               isQueensideCastling = false,
		char               promotionPiece      = '\0')
	{
		Span<char> board = stackalloc char[64];
		state.Board.AsSpan().CopyTo(board);
		char movingPiece = board[fromIndex];
		board[fromIndex] = '\0';

		if (isEnPassant)
			board[state.ActiveColor == 'w' ? toIndex - 8 : toIndex + 8] = '\0';

		if (isKingsideCastling)
			MoveRookForCastling(board, toIndex, true);
		else if (isQueensideCastling)
			MoveRookForCastling(board, toIndex, false);

		board[toIndex] = promotionPiece == '\0'
			? movingPiece
			: ColorizePromotionPiece(promotionPiece, state.ActiveColor);
		return !IsKingInCheck(board, state.ActiveColor);
	}

	private static bool IsKingInCheck(ReadOnlySpan<char> board, char color)
	{
		char king = color == 'w' ? 'K' : 'k';
		for (var index = 0; index < board.Length; index++)
		{
			if (board[index] == king)
				return IsSquareAttacked(board, index, Opposite(color));
		}

		return false;
	}

	private static bool IsFriendlyPiece(char piece, char color) =>
		piece != '\0' && GetPieceColor(piece) == color;

	private static bool IsEnemyPiece(char piece, char color) =>
		piece != '\0' && GetPieceColor(piece) != color;

	private static char GetPieceColor(char piece) => char.IsUpper(piece) ? 'w' : 'b';

	private static bool IsSquareAttacked(ReadOnlySpan<char> board, int squareIndex, char attackingColor)
	{
		int file = GetFile(squareIndex);
		int rank = GetRank(squareIndex);
		int pawnRank = attackingColor == 'w' ? rank - 1 : rank + 1;
		if (IsOnBoard(file - 1, pawnRank) &&
			board[GetIndex(file - 1, pawnRank)] == (attackingColor == 'w' ? 'P' : 'p'))
			return true;

		if (IsOnBoard(file + 1, pawnRank) &&
			board[GetIndex(file + 1, pawnRank)] == (attackingColor == 'w' ? 'P' : 'p'))
			return true;

		foreach ((int fileDelta, int rankDelta) in KnightOffsets)
		{
			int attackerFile = file + fileDelta;
			int attackerRank = rank + rankDelta;
			if (IsOnBoard(attackerFile, attackerRank) &&
				board[GetIndex(attackerFile, attackerRank)] == (attackingColor == 'w' ? 'N' : 'n'))
				return true;
		}

		if (IsAttackedBySlidingPiece(board, file, rank, attackingColor, DiagonalDirections, "bq") ||
			IsAttackedBySlidingPiece(board, file, rank, attackingColor, OrthogonalDirections, "rq"))
			return true;

		foreach ((int fileDelta, int rankDelta) in KingOffsets)
		{
			int attackerFile = file + fileDelta;
			int attackerRank = rank + rankDelta;
			if (IsOnBoard(attackerFile, attackerRank) &&
				board[GetIndex(attackerFile, attackerRank)] == (attackingColor == 'w' ? 'K' : 'k'))
				return true;
		}

		return false;
	}

	private static bool IsAttackedBySlidingPiece(
		ReadOnlySpan<char>     board,
		int                    file,
		int                    rank,
		char                   attackingColor,
		(int File, int Rank)[] directions,
		ReadOnlySpan<char>     attackerPieces)
	{
		foreach ((int fileDelta, int rankDelta) in directions)
		{
			int attackerFile = file + fileDelta;
			int attackerRank = rank + rankDelta;
			while (IsOnBoard(attackerFile, attackerRank))
			{
				char piece = board[GetIndex(attackerFile, attackerRank)];
				if (piece == '\0')
				{
					attackerFile += fileDelta;
					attackerRank += rankDelta;
					continue;
				}

				if (GetPieceColor(piece) == attackingColor &&
					attackerPieces.IndexOf(char.ToLowerInvariant(piece)) >= 0)
					return true;

				break;
			}
		}

		return false;
	}
}
