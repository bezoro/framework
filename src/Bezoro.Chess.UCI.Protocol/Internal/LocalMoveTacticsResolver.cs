namespace Bezoro.Chess.UCI.Protocol.Internal;

internal static class LocalMoveTacticsResolver
{
	private static readonly (int File, int Rank)[] DiagonalDirections =
	[
		(-1, -1), (-1, 1), (1, -1), (1, 1)
	];

	private static readonly (int File, int Rank)[] KingOffsets =
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

	public static MoveClassification Resolve(Fen fen, string move, MoveClassification structural)
	{
		var  current       = ParseState(fen);
		var  next          = ApplyMove(current, move, structural);
		bool isCheck       = IsKingInCheck(next.Board, next.ActiveColor);
		bool hasLegalReply = HasAnyLegalMove(next);
		return structural.WithTacticalOutcome(
			isCheck,
			isCheck && !hasLegalReply,
			!isCheck && !hasLegalReply
		);
	}

	private static bool CanCastle(LocalPositionState state, bool kingside)
	{
		char king      = state.ActiveColor == 'w' ? 'K' : 'k';
		int  kingIndex = Array.IndexOf(state.Board, king);
		if (kingIndex < 0 || IsKingInCheck(state.Board, state.ActiveColor))
			return false;

		string requiredRight = state.ActiveColor == 'w'
								   ? kingside ? "K" : "Q"
								   : kingside
									   ? "k"
									   : "q";

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

			return IsLegalMove(
				state,
				kingIndex,
				GetIndex(6, rank),
				isKingsideCastling: true
			);
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

		return IsLegalMove(
			state,
			kingIndex,
			GetIndex(2, rank),
			isQueensideCastling: true
		);
	}

	private static bool HasAnyLegalKingMove(LocalPositionState state, int fromIndex)
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
				return true;
		}

		if (CanCastle(state, true) || CanCastle(state, false))
			return true;

		return false;
	}

	private static bool HasAnyLegalKnightMove(LocalPositionState state, int fromIndex)
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
				return true;
		}

		return false;
	}

	private static bool HasAnyLegalMove(LocalPositionState state)
	{
		for (var fromIndex = 0; fromIndex < 64; fromIndex++)
		{
			char piece = state.Board[fromIndex];
			if (piece == '\0' || GetPieceColor(piece) != state.ActiveColor)
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
					if (HasAnyLegalSlidingMove(state, fromIndex, DiagonalDirections))
						return true;

					break;
				case 'r':
					if (HasAnyLegalSlidingMove(state, fromIndex, OrthogonalDirections))
						return true;

					break;
				case 'q':
					if (HasAnyLegalSlidingMove(state, fromIndex, QueenDirections))
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
		int file          = GetFile(fromIndex);
		int rank          = GetRank(fromIndex);
		int forward       = state.ActiveColor == 'w' ? 1 : -1;
		int promotionRank = state.ActiveColor == 'w' ? 7 : 0;
		int startRank     = state.ActiveColor == 'w' ? 1 : 6;
		int nextRank      = rank + forward;

		if (IsOnBoard(file, nextRank))
		{
			int oneForwardIndex = GetIndex(file, nextRank);
			if (state.Board[oneForwardIndex] == '\0')
			{
				char promotionPiece = nextRank == promotionRank ? 'q' : '\0';
				if (IsLegalMove(state, fromIndex, oneForwardIndex, promotionPiece: promotionPiece))
					return true;

				if (rank == startRank)
				{
					int twoForwardRank  = rank + 2 * forward;
					int twoForwardIndex = GetIndex(file, twoForwardRank);
					if (state.Board[twoForwardIndex] == '\0' &&
						IsLegalMove(state, fromIndex, twoForwardIndex))
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

				char promotionPiece = nextRank == promotionRank ? 'q' : '\0';
				if (IsLegalMove(state, fromIndex, targetIndex, isEnPassant, promotionPiece: promotionPiece))
					return true;
			}
		}

		return false;
	}

	private static bool HasAnyLegalSlidingMove(
		LocalPositionState     state,
		int                    fromIndex,
		(int File, int Rank)[] directions)
	{
		int file = GetFile(fromIndex);
		int rank = GetRank(fromIndex);

		foreach ((int fileDelta, int rankDelta) in directions)
		{
			int targetFile = file + fileDelta;
			int targetRank = rank + rankDelta;
			while (IsOnBoard(targetFile, targetRank))
			{
				int  targetIndex = GetIndex(targetFile, targetRank);
				char targetPiece = state.Board[targetIndex];
				if (IsFriendlyPiece(targetPiece, state.ActiveColor))
					break;

				if (IsLegalMove(state, fromIndex, targetIndex))
					return true;

				if (targetPiece != '\0')
					break;

				targetFile += fileDelta;
				targetRank += rankDelta;
			}
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

	private static bool IsEnemyPiece(char piece, char color) => piece != '\0' && GetPieceColor(piece) != color;

	private static bool IsFriendlyPiece(char piece, char color) => piece != '\0' && GetPieceColor(piece) == color;

	private static bool IsKingInCheck(char[] board, char color)
	{
		char king      = color == 'w' ? 'K' : 'k';
		int  kingIndex = Array.IndexOf(board, king);
		return kingIndex >= 0 && IsSquareAttacked(board, kingIndex, Opposite(color));
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
		var  board       = (char[])state.Board.Clone();
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

	private static bool IsOnBoard(int file, int rank) => file is >= 0 and < 8 && rank is >= 0 and < 8;

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

	private static char ColorizePromotionPiece(char promotionPiece, char color)
	{
		var normalizedPiece = char.ToLowerInvariant(promotionPiece);
		return color == 'w' ? char.ToUpperInvariant(normalizedPiece) : normalizedPiece;
	}

	private static char GetPieceColor(char piece) => char.IsUpper(piece) ? 'w' : 'b';

	private static char Opposite(char color) => color == 'w' ? 'b' : 'w';

	private static int GetFile(int index) => index % 8;

	private static int GetIndex(int file, int rank) => rank * 8 + file;

	private static int GetRank(int index) => index / 8;

	private static int ParseSquare(ReadOnlySpan<char> square) => GetIndex(square[0] - 'a', square[1] - '1');

	private static LocalPositionState ApplyMove(LocalPositionState state, string move, MoveClassification structural)
	{
		var  board       = (char[])state.Board.Clone();
		int  fromIndex   = ParseSquare(move.AsSpan(0, 2));
		int  toIndex     = ParseSquare(move.AsSpan(2, 2));
		char movingPiece = board[fromIndex];
		char targetPiece = board[toIndex];

		board[fromIndex] = '\0';

		if (structural.IsEnPassant)
		{
			int capturedPawnIndex = state.ActiveColor == 'w' ? toIndex - 8 : toIndex + 8;
			board[capturedPawnIndex] = '\0';
		}

		if (structural.IsKingsideCastling)
			MoveRookForCastling(board, toIndex, true);
		else if (structural.IsQueensideCastling)
			MoveRookForCastling(board, toIndex, false);

		board[toIndex] = structural.IsPromotion
							 ? ColorizePromotionPiece(move[4], state.ActiveColor)
							 : movingPiece;

		string castlingRights = UpdateCastlingRights(
			state.CastlingRights, movingPiece, fromIndex, targetPiece, toIndex
		);

		string enPassantTarget = structural.IsDoublePawnPush
									 ? FormatSquare((fromIndex + toIndex) / 2)
									 : string.Empty;

		return new(board, Opposite(state.ActiveColor), castlingRights, enPassantTarget);
	}

	private static LocalPositionState ParseState(Fen fen)
	{
		var      board = new char[64];
		string[] ranks = fen.PiecePlacement.Split('/');
		if (ranks.Length != 8)
			throw new ArgumentException("Piece placement must contain eight ranks.", nameof(fen));

		for (var rankIndex = 0; rankIndex < 8; rankIndex++)
		{
			var fileIndex = 0;
			foreach (char symbol in ranks[rankIndex])
			{
				if (char.IsDigit(symbol))
				{
					fileIndex += symbol - '0';
					continue;
				}

				board[GetIndex(fileIndex, 7 - rankIndex)] = symbol;
				fileIndex++;
			}
		}

		return new(
			board,
			fen.ActiveColor,
			NormalizeCastlingRights(fen.CastlingRights),
			NormalizeEnPassantTarget(fen.EnPassantTarget)
		);
	}

	private static string FormatSquare(int index) => $"{(char)('a' + GetFile(index))}{(char)('1' + GetRank(index))}";

	private static string NormalizeCastlingRights(string castlingRights) =>
		string.Equals(castlingRights, "-", StringComparison.Ordinal) ? string.Empty : castlingRights;

	private static string NormalizeEnPassantTarget(string enPassantTarget) =>
		string.Equals(enPassantTarget, "-", StringComparison.Ordinal)
			? string.Empty
			: enPassantTarget.ToLowerInvariant();

	private static string RemoveCastlingRights(string castlingRights, string rightsToRemove)
	{
		if (string.IsNullOrEmpty(castlingRights))
			return string.Empty;

		string updated = castlingRights;
		foreach (char right in rightsToRemove)
			updated = updated.Replace(right.ToString(), string.Empty, StringComparison.Ordinal);

		return updated;
	}

	private static string UpdateCastlingRights(
		string castlingRights,
		char   movingPiece,
		int    fromIndex,
		char   capturedPiece,
		int    toIndex)
	{
		string updated = castlingRights;
		switch (movingPiece)
		{
			case 'K':
				updated = RemoveCastlingRights(updated, "KQ");
				break;
			case 'k':
				updated = RemoveCastlingRights(updated, "kq");
				break;
			case 'R':
				if (fromIndex == GetIndex(0, 0))
					updated = RemoveCastlingRights(updated, "Q");
				else if (fromIndex == GetIndex(7, 0))
					updated = RemoveCastlingRights(updated, "K");

				break;
			case 'r':
				if (fromIndex == GetIndex(0, 7))
					updated = RemoveCastlingRights(updated, "q");
				else if (fromIndex == GetIndex(7, 7))
					updated = RemoveCastlingRights(updated, "k");

				break;
		}

		switch (capturedPiece)
		{
			case 'R':
				if (toIndex == GetIndex(0, 0))
					updated = RemoveCastlingRights(updated, "Q");
				else if (toIndex == GetIndex(7, 0))
					updated = RemoveCastlingRights(updated, "K");

				break;
			case 'r':
				if (toIndex == GetIndex(0, 7))
					updated = RemoveCastlingRights(updated, "q");
				else if (toIndex == GetIndex(7, 7))
					updated = RemoveCastlingRights(updated, "k");

				break;
		}

		return updated;
	}

	private static void MoveRookForCastling(char[] board, int kingDestinationIndex, bool kingside)
	{
		int rank          = GetRank(kingDestinationIndex);
		int rookFromIndex = kingside ? GetIndex(7, rank) : GetIndex(0, rank);
		int rookToIndex   = kingside ? GetIndex(5, rank) : GetIndex(3, rank);
		board[rookToIndex]   = board[rookFromIndex];
		board[rookFromIndex] = '\0';
	}

	private readonly record struct LocalPositionState(
		char[] Board,
		char   ActiveColor,
		string CastlingRights,
		string EnPassantTarget
	);
}
