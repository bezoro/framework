namespace Bezoro.Chess.UCI.Protocol.Internal;

internal static class LocalMoveTacticsResolver
{
	internal static MoveClassification Resolve(
		LocalPositionState state,
		string             move,
		MoveClassification structural)
	{
		var next = LocalPositionRules.ApplyMove(state, move, structural);
		bool isCheck = LocalPositionRules.IsKingInCheck(next.Board, next.ActiveColor);
		bool hasLegalReply = HasAnyLegalMove(next);
		return structural.WithTacticalOutcome(
			isCheck,
			isCheck && !hasLegalReply,
			!isCheck && !hasLegalReply
		);
	}

	private static bool HasAnyLegalMove(LocalPositionState state)
	{
		for (var fromIndex = 0; fromIndex < 64; fromIndex++)
		{
			char piece = state.Board[fromIndex];
			if (piece == '\0' || LocalPositionRules.GetPieceColor(piece) != state.ActiveColor)
				continue;

			switch (char.ToLowerInvariant(piece))
			{
				case 'p':
					if (HasAnyLegalPawnMove(state, fromIndex))
						return true;
					break;
				case 'n':
					if (HasAnyLegalKnightMove(state, fromIndex))
						return true;
					break;
				case 'b':
					if (HasAnyLegalSlidingMove(state, fromIndex, LocalPositionRules.DiagonalDirections))
						return true;
					break;
				case 'r':
					if (HasAnyLegalSlidingMove(state, fromIndex, LocalPositionRules.OrthogonalDirections))
						return true;
					break;
				case 'q':
					if (HasAnyLegalSlidingMove(state, fromIndex, LocalPositionRules.QueenDirections))
						return true;
					break;
				case 'k':
					if (HasAnyLegalKingMove(state, fromIndex))
						return true;
					break;
			}
		}

		return false;
	}

	private static bool HasAnyLegalPawnMove(LocalPositionState state, int fromIndex)
	{
		int file = LocalPositionRules.GetFile(fromIndex);
		int rank = LocalPositionRules.GetRank(fromIndex);
		int forward = state.ActiveColor == 'w' ? 1 : -1;
		int promotionRank = state.ActiveColor == 'w' ? 7 : 0;
		int startRank = state.ActiveColor == 'w' ? 1 : 6;
		int nextRank = rank + forward;

		if (!LocalPositionRules.IsOnBoard(file, nextRank))
			return false;

		int oneForwardIndex = LocalPositionRules.GetIndex(file, nextRank);
		if (state.Board[oneForwardIndex] == '\0')
		{
			char promotionPiece = nextRank == promotionRank ? 'q' : '\0';
			if (LocalPositionRules.IsLegalMove(
					state,
					fromIndex,
					oneForwardIndex,
					promotionPiece: promotionPiece))
				return true;

			if (rank == startRank)
			{
				int twoForwardRank = rank + (2 * forward);
				int twoForwardIndex = LocalPositionRules.GetIndex(file, twoForwardRank);
				if (state.Board[twoForwardIndex] == '\0' &&
					LocalPositionRules.IsLegalMove(state, fromIndex, twoForwardIndex))
					return true;
			}
		}

		for (int fileDelta = -1; fileDelta <= 1; fileDelta += 2)
		{
			int targetFile = file + fileDelta;
			if (!LocalPositionRules.IsOnBoard(targetFile, nextRank))
				continue;

			int targetIndex = LocalPositionRules.GetIndex(targetFile, nextRank);
			bool isEnPassant = string.Equals(
				state.EnPassantTarget,
				LocalPositionRules.FormatSquare(targetIndex),
				StringComparison.Ordinal
			);

			if (!isEnPassant && !LocalPositionRules.IsEnemyPiece(state.Board[targetIndex], state.ActiveColor))
				continue;

			char promotionPiece = nextRank == promotionRank ? 'q' : '\0';
			if (LocalPositionRules.IsLegalMove(
					state,
					fromIndex,
					targetIndex,
					isEnPassant,
					promotionPiece: promotionPiece))
				return true;
		}

		return false;
	}

	private static bool HasAnyLegalKnightMove(LocalPositionState state, int fromIndex)
	{
		int file = LocalPositionRules.GetFile(fromIndex);
		int rank = LocalPositionRules.GetRank(fromIndex);

		foreach ((int fileDelta, int rankDelta) in LocalPositionRules.KnightOffsets)
		{
			int targetFile = file + fileDelta;
			int targetRank = rank + rankDelta;
			if (!LocalPositionRules.IsOnBoard(targetFile, targetRank))
				continue;

			int targetIndex = LocalPositionRules.GetIndex(targetFile, targetRank);
			if (LocalPositionRules.IsFriendlyPiece(state.Board[targetIndex], state.ActiveColor))
				continue;

			if (LocalPositionRules.IsLegalMove(state, fromIndex, targetIndex))
				return true;
		}

		return false;
	}

	private static bool HasAnyLegalSlidingMove(
		LocalPositionState     state,
		int                    fromIndex,
		(int File, int Rank)[] directions)
	{
		int file = LocalPositionRules.GetFile(fromIndex);
		int rank = LocalPositionRules.GetRank(fromIndex);

		foreach ((int fileDelta, int rankDelta) in directions)
		{
			int targetFile = file + fileDelta;
			int targetRank = rank + rankDelta;
			while (LocalPositionRules.IsOnBoard(targetFile, targetRank))
			{
				int targetIndex = LocalPositionRules.GetIndex(targetFile, targetRank);
				char targetPiece = state.Board[targetIndex];
				if (LocalPositionRules.IsFriendlyPiece(targetPiece, state.ActiveColor))
					break;

				if (LocalPositionRules.IsLegalMove(state, fromIndex, targetIndex))
					return true;

				if (targetPiece != '\0')
					break;

				targetFile += fileDelta;
				targetRank += rankDelta;
			}
		}

		return false;
	}

	private static bool HasAnyLegalKingMove(LocalPositionState state, int fromIndex)
	{
		int file = LocalPositionRules.GetFile(fromIndex);
		int rank = LocalPositionRules.GetRank(fromIndex);

		foreach ((int fileDelta, int rankDelta) in LocalPositionRules.KingOffsets)
		{
			int targetFile = file + fileDelta;
			int targetRank = rank + rankDelta;
			if (!LocalPositionRules.IsOnBoard(targetFile, targetRank))
				continue;

			int targetIndex = LocalPositionRules.GetIndex(targetFile, targetRank);
			if (LocalPositionRules.IsFriendlyPiece(state.Board[targetIndex], state.ActiveColor))
				continue;

			if (LocalPositionRules.IsLegalMove(state, fromIndex, targetIndex))
				return true;
		}

		return LocalPositionRules.CanCastle(state, true) || LocalPositionRules.CanCastle(state, false);
	}
}
