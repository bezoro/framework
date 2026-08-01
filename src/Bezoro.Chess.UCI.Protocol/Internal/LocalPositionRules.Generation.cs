using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.Internal;

internal static partial class LocalPositionRules
{
	internal static readonly (int File, int Rank)[] DiagonalDirections =
	[
		(-1, -1), (-1, 1), (1, -1), (1, 1)
	];

	internal static readonly (int File, int Rank)[] OrthogonalDirections =
	[
		(-1, 0), (1, 0), (0, -1), (0, 1)
	];

	internal static readonly (int File, int Rank)[] QueenDirections =
	[
		(-1, -1), (-1, 0), (-1, 1),
		(0, -1), (0, 1),
		(1, -1), (1, 0), (1, 1)
	];

	internal static readonly (int File, int Rank)[] KnightOffsets =
	[
		(-2, -1), (-2, 1), (-1, -2), (-1, 2),
		(1, -2), (1, 2), (2, -1), (2, 1)
	];

	internal static readonly (int File, int Rank)[] KingOffsets =
	[
		(-1, -1), (-1, 0), (-1, 1),
		(0, -1), (0, 1),
		(1, -1), (1, 0), (1, 1)
	];

	internal static ImmutableArray<string> GenerateLegalMoves(LocalPositionState state)
	{
		var builder = ImmutableArray.CreateBuilder<string>(64);

		for (var fromIndex = 0; fromIndex < 64; fromIndex++)
		{
			char piece = state.Board[fromIndex];
			if (piece == '\0' || GetPieceColor(piece) != state.ActiveColor)
				continue;

			switch (char.ToLowerInvariant(piece))
			{
				case 'p':
					AddLegalPawnMoves(state, fromIndex, builder);
					break;
				case 'n':
					AddLegalKnightMoves(state, fromIndex, builder);
					break;
				case 'b':
					AddLegalSlidingMoves(state, fromIndex, builder, DiagonalDirections);
					break;
				case 'r':
					AddLegalSlidingMoves(state, fromIndex, builder, OrthogonalDirections);
					break;
				case 'q':
					AddLegalSlidingMoves(state, fromIndex, builder, QueenDirections);
					break;
				case 'k':
					AddLegalKingMoves(state, fromIndex, builder);
					break;
			}
		}

		return builder.ToImmutable();
	}

	private static void AddLegalPawnMoves(
		LocalPositionState           state,
		int                          fromIndex,
		ImmutableArray<string>.Builder builder)
	{
		int file = GetFile(fromIndex);
		int rank = GetRank(fromIndex);
		int forward = state.ActiveColor == 'w' ? 1 : -1;
		int nextRank = rank + forward;
		int promotionRank = state.ActiveColor == 'w' ? 7 : 0;
		int startRank = state.ActiveColor == 'w' ? 1 : 6;

		if (!IsOnBoard(file, nextRank))
			return;

		int oneForwardIndex = GetIndex(file, nextRank);
		if (state.Board[oneForwardIndex] == '\0')
		{
			AddPawnMoveIfLegal(state, fromIndex, oneForwardIndex, nextRank == promotionRank, builder);

			if (rank == startRank)
			{
				int twoForwardRank = rank + (2 * forward);
				int twoForwardIndex = GetIndex(file, twoForwardRank);
				if (state.Board[twoForwardIndex] == '\0' && IsLegalMove(state, fromIndex, twoForwardIndex))
					builder.Add(FormatMove(fromIndex, twoForwardIndex));
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

			AddPawnMoveIfLegal(state, fromIndex, targetIndex, nextRank == promotionRank, builder, isEnPassant);
		}
	}

	private static void AddPawnMoveIfLegal(
		LocalPositionState           state,
		int                          fromIndex,
		int                          toIndex,
		bool                         isPromotion,
		ImmutableArray<string>.Builder builder,
		bool                         isEnPassant = false)
	{
		if (!isPromotion)
		{
			if (IsLegalMove(state, fromIndex, toIndex, isEnPassant))
				builder.Add(FormatMove(fromIndex, toIndex));

			return;
		}

		foreach (char promotionPiece in "qrbn")
		{
			if (IsLegalMove(state, fromIndex, toIndex, isEnPassant, promotionPiece: promotionPiece))
				builder.Add(FormatMove(fromIndex, toIndex, promotionPiece));
		}
	}

	private static void AddLegalKnightMoves(
		LocalPositionState           state,
		int                          fromIndex,
		ImmutableArray<string>.Builder builder)
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
			if (IsFriendlyPiece(state.Board[targetIndex], state.ActiveColor))
				continue;

			if (IsLegalMove(state, fromIndex, targetIndex))
				builder.Add(FormatMove(fromIndex, targetIndex));
		}
	}

	private static void AddLegalSlidingMoves(
		LocalPositionState           state,
		int                          fromIndex,
		ImmutableArray<string>.Builder builder,
		(int File, int Rank)[]       directions)
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

				if (IsLegalMove(state, fromIndex, targetIndex))
					builder.Add(FormatMove(fromIndex, targetIndex));

				if (targetPiece != '\0')
					break;

				targetFile += fileDelta;
				targetRank += rankDelta;
			}
		}
	}

	private static void AddLegalKingMoves(
		LocalPositionState           state,
		int                          fromIndex,
		ImmutableArray<string>.Builder builder)
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
			if (IsFriendlyPiece(state.Board[targetIndex], state.ActiveColor))
				continue;

			if (IsLegalMove(state, fromIndex, targetIndex))
				builder.Add(FormatMove(fromIndex, targetIndex));
		}

		if (CanCastle(state, true))
			builder.Add(FormatMove(fromIndex, GetIndex(6, rank)));

		if (CanCastle(state, false))
			builder.Add(FormatMove(fromIndex, GetIndex(2, rank)));
	}

	internal static bool CanCastle(LocalPositionState state, bool kingside)
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
			if (state.Board[GetIndex(5, rank)] != '\0' || state.Board[GetIndex(6, rank)] != '\0')
				return false;

			if (state.Board[GetIndex(7, rank)] != (state.ActiveColor == 'w' ? 'R' : 'r'))
				return false;

			if (IsSquareAttacked(state.Board, GetIndex(5, rank), Opposite(state.ActiveColor)) ||
				IsSquareAttacked(state.Board, GetIndex(6, rank), Opposite(state.ActiveColor)))
				return false;

			return IsLegalMove(state, kingIndex, GetIndex(6, rank), isKingsideCastling: true);
		}

		if (state.Board[GetIndex(1, rank)] != '\0' ||
			state.Board[GetIndex(2, rank)] != '\0' ||
			state.Board[GetIndex(3, rank)] != '\0')
			return false;

		if (state.Board[GetIndex(0, rank)] != (state.ActiveColor == 'w' ? 'R' : 'r'))
			return false;

		if (IsSquareAttacked(state.Board, GetIndex(3, rank), Opposite(state.ActiveColor)) ||
			IsSquareAttacked(state.Board, GetIndex(2, rank), Opposite(state.ActiveColor)))
			return false;

		return IsLegalMove(state, kingIndex, GetIndex(2, rank), isQueensideCastling: true);
	}

	internal static bool IsLegalMove(
		LocalPositionState state,
		int                fromIndex,
		int                toIndex,
		bool               isEnPassant         = false,
		bool               isKingsideCastling  = false,
		bool               isQueensideCastling = false,
		char               promotionPiece      = '\0')
	{
		var board = (char[])state.Board.Clone();
		char movingPiece = board[fromIndex];
		board[fromIndex] = '\0';

		if (isEnPassant)
		{
			int capturedPawnIndex = state.ActiveColor == 'w' ? toIndex - 8 : toIndex + 8;
			board[capturedPawnIndex] = '\0';
		}

		if (isKingsideCastling)
			MoveRookForCastling(board, toIndex, true);
		else if (isQueensideCastling)
			MoveRookForCastling(board, toIndex, false);

		board[toIndex] = promotionPiece == '\0'
			? movingPiece
			: ColorizePromotionPiece(promotionPiece, state.ActiveColor);

		return !IsKingInCheck(board, state.ActiveColor);
	}

	internal static bool IsKingInCheck(char[] board, char color)
	{
		char king = color == 'w' ? 'K' : 'k';
		int kingIndex = Array.IndexOf(board, king);
		return kingIndex >= 0 && IsSquareAttacked(board, kingIndex, Opposite(color));
	}

	internal static bool IsFriendlyPiece(char piece, char color) =>
		piece != '\0' && GetPieceColor(piece) == color;

	internal static bool IsEnemyPiece(char piece, char color) =>
		piece != '\0' && GetPieceColor(piece) != color;

	internal static char GetPieceColor(char piece) => char.IsUpper(piece) ? 'w' : 'b';

	private static bool IsSquareAttacked(char[] board, int squareIndex, char attackingColor)
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
			if (!IsOnBoard(attackerFile, attackerRank))
				continue;

			char piece = board[GetIndex(attackerFile, attackerRank)];
			if (piece == (attackingColor == 'w' ? 'N' : 'n'))
				return true;
		}

		if (IsAttackedBySlidingPiece(board, file, rank, attackingColor, DiagonalDirections, ['b', 'q']))
			return true;

		if (IsAttackedBySlidingPiece(board, file, rank, attackingColor, OrthogonalDirections, ['r', 'q']))
			return true;

		foreach ((int fileDelta, int rankDelta) in KingOffsets)
		{
			int attackerFile = file + fileDelta;
			int attackerRank = rank + rankDelta;
			if (!IsOnBoard(attackerFile, attackerRank))
				continue;

			char piece = board[GetIndex(attackerFile, attackerRank)];
			if (piece == (attackingColor == 'w' ? 'K' : 'k'))
				return true;
		}

		return false;
	}

	private static bool IsAttackedBySlidingPiece(
		char[]                 board,
		int                    file,
		int                    rank,
		char                   attackingColor,
		(int File, int Rank)[] directions,
		char[]                 attackerPieces)
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
					Array.IndexOf(attackerPieces, char.ToLowerInvariant(piece)) >= 0)
					return true;

				break;
			}
		}

		return false;
	}
}
