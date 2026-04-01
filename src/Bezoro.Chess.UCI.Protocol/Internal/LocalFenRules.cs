using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.API.Types;

namespace Bezoro.Chess.UCI.Protocol.Internal;

internal static class LocalFenRules
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

	public static Fen ApplyMove(Fen fen, string move)
	{
		if (string.IsNullOrWhiteSpace(move))
			throw new ArgumentException("Move must not be blank.", nameof(move));

		string normalizedMove = move.Trim().ToLowerInvariant();
		if (!UciEngineClient.IsUciMoveString(normalizedMove))
			throw new ArgumentException("Move must be valid UCI notation.", nameof(move));

		var state = ParseState(fen);
		if (!GenerateLegalMoves(state).Contains(normalizedMove, StringComparer.Ordinal))
			throw new InvalidOperationException("The move is not legal in the supplied FEN position.");

		var structural = fen.ClassifyMove(normalizedMove);
		return ApplyMove(state, normalizedMove, structural);
	}

	public static ImmutableArray<string> GetLegalMoves(Fen fen) => GenerateLegalMoves(ParseState(fen));

	public static bool IsCurrentPlayerInCheck(Fen fen)
	{
		var state = ParseState(fen);
		return IsKingInCheck(state.Board, state.ActiveColor);
	}

	public static bool TryCreatePendingPromotion(
		Fen                    fen,
		string                 move,
		ImmutableArray<string> legalMoves,
		out PendingPromotionRequest request)
	{
		request = default;
		if (string.IsNullOrWhiteSpace(move))
			return false;

		string normalizedMove = move.Trim().ToLowerInvariant();
		if (normalizedMove.Length != 4 || !UciEngineClient.IsUciMoveString(normalizedMove + "q"))
			return false;

		var promotionChoices = "qrbn"
			.Where(candidate => legalMoves.Contains(normalizedMove + candidate, StringComparer.Ordinal))
			.ToImmutableArray();

		if (promotionChoices.IsDefaultOrEmpty)
			return false;

		var board = ParseState(fen).Board;
		int fromIndex = ParseSquare(normalizedMove.AsSpan(0, 2));
		char movingPiece = board[fromIndex];

		request = new(
			normalizedMove,
			fen.ActiveColor,
			movingPiece,
			normalizedMove[..2],
			normalizedMove.Substring(2, 2),
			fen.Raw,
			fen,
			promotionChoices
		);

		return true;
	}

	public static bool HasInsufficientMaterial(Fen fen)
	{
		var state = ParseState(fen);
		var bishops = new List<(char Piece, int Square)>();
		var knights = new List<char>();
		var majorsOrPawns = new List<char>();

		foreach (char piece in state.Board)
		{
			if (piece == '\0' || char.ToLowerInvariant(piece) == 'k')
				continue;

			switch (char.ToLowerInvariant(piece))
			{
				case 'b':
					bishops.Add((piece, Array.IndexOf(state.Board, piece)));
					break;
				case 'n':
					knights.Add(piece);
					break;
				default:
					majorsOrPawns.Add(piece);
					break;
			}
		}

		if (majorsOrPawns.Count > 0)
			return false;

		if (bishops.Count == 0 && knights.Count == 0)
			return true;

		if (bishops.Count == 0 && knights.Count == 1)
			return true;

		if (bishops.Count == 1 && knights.Count == 0)
			return true;

		if (bishops.Count == 2 && knights.Count == 0)
		{
			bool sameColor = IsLightSquare(bishops[0].Square) == IsLightSquare(bishops[1].Square);
			bool oppositeSides = char.IsUpper(bishops[0].Piece) != char.IsUpper(bishops[1].Piece);
			return sameColor && oppositeSides;
		}

		return false;
	}

	public static string BuildRepetitionKey(Fen fen) =>
		$"{fen.PiecePlacement} {fen.ActiveColor} {NormalizeCastlingRights(fen.CastlingRights)} {NormalizeEnPassantTarget(fen.EnPassantTarget)}";

	private static ImmutableArray<string> GenerateLegalMoves(LocalFenState state)
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

	private static void AddLegalPawnMoves(LocalFenState state, int fromIndex, ImmutableArray<string>.Builder builder)
	{
		int file = GetFile(fromIndex);
		int rank = GetRank(fromIndex);
		int forward = state.ActiveColor == 'w' ? 1 : -1;
		int nextRank = rank + forward;
		int promotionRank = state.ActiveColor == 'w' ? 7 : 0;
		int startRank = state.ActiveColor == 'w' ? 1 : 6;

		if (IsOnBoard(file, nextRank))
		{
			int oneForwardIndex = GetIndex(file, nextRank);
			if (state.Board[oneForwardIndex] == '\0')
			{
				AddPawnMoveIfLegal(state, fromIndex, oneForwardIndex, nextRank == promotionRank, builder);

				if (rank == startRank)
				{
					int twoForwardRank = rank + (2 * forward);
					int twoForwardIndex = GetIndex(file, twoForwardRank);
					if (state.Board[twoForwardIndex] == '\0' &&
						IsLegalMove(state, fromIndex, twoForwardIndex))
					{
						builder.Add(FormatMove(fromIndex, twoForwardIndex));
					}
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
	}

	private static void AddPawnMoveIfLegal(
		LocalFenState                  state,
		int                            fromIndex,
		int                            toIndex,
		bool                           isPromotion,
		ImmutableArray<string>.Builder builder,
		bool                           isEnPassant = false)
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

	private static void AddLegalKnightMoves(LocalFenState state, int fromIndex, ImmutableArray<string>.Builder builder)
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
		LocalFenState                  state,
		int                            fromIndex,
		ImmutableArray<string>.Builder builder,
		(int File, int Rank)[]         directions)
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

	private static void AddLegalKingMoves(LocalFenState state, int fromIndex, ImmutableArray<string>.Builder builder)
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

	private static bool CanCastle(LocalFenState state, bool kingside)
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

	private static bool IsLegalMove(
		LocalFenState state,
		int           fromIndex,
		int           toIndex,
		bool          isEnPassant         = false,
		bool          isKingsideCastling  = false,
		bool          isQueensideCastling = false,
		char          promotionPiece      = '\0')
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

	private static Fen ApplyMove(LocalFenState state, string move, MoveClassification structural)
	{
		var board = (char[])state.Board.Clone();
		int fromIndex = ParseSquare(move.AsSpan(0, 2));
		int toIndex = ParseSquare(move.AsSpan(2, 2));
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
			state.CastlingRights,
			movingPiece,
			fromIndex,
			targetPiece,
			toIndex
		);

		string enPassantTarget = structural.IsDoublePawnPush
			? FormatSquare((fromIndex + toIndex) / 2)
			: "-";

		int nextHalfmoveClock = char.ToLowerInvariant(movingPiece) == 'p' || structural.IsCapture
			? 0
			: state.HalfmoveClock + 1;

		int nextFullmoveNumber = state.ActiveColor == 'b'
			? state.FullmoveNumber + 1
			: state.FullmoveNumber;

		string rawFen = string.Create(
			CultureInvariantLength(board, castlingRights, enPassantTarget, nextHalfmoveClock, nextFullmoveNumber),
			(
				board,
				activeColor: Opposite(state.ActiveColor),
				castlingRights,
				enPassantTarget,
				halfmoveClock: nextHalfmoveClock,
				fullmoveNumber: nextFullmoveNumber
			),
			static (span, value) =>
			{
				string raw = BuildRawFen(
					value.board,
					value.activeColor,
					value.castlingRights,
					value.enPassantTarget,
					value.halfmoveClock,
					value.fullmoveNumber
				);
				raw.AsSpan().CopyTo(span);
			}
		);

		return Fen.Parse(rawFen)!.Value;
	}

	private static string BuildRawFen(
		char[] board,
		char   activeColor,
		string castlingRights,
		string enPassantTarget,
		int    halfmoveClock,
		int    fullmoveNumber)
	{
		var builder = new StringBuilder(96);
		for (var rank = 7; rank >= 0; rank--)
		{
			var emptyCount = 0;
			for (var file = 0; file < 8; file++)
			{
				char piece = board[GetIndex(file, rank)];
				if (piece == '\0')
				{
					emptyCount++;
					continue;
				}

				if (emptyCount > 0)
				{
					builder.Append(emptyCount);
					emptyCount = 0;
				}

				builder.Append(piece);
			}

			if (emptyCount > 0)
				builder.Append(emptyCount);

			if (rank > 0)
				builder.Append('/');
		}

		builder.Append(' ');
		builder.Append(activeColor);
		builder.Append(' ');
		builder.Append(string.IsNullOrEmpty(castlingRights) ? "-" : castlingRights);
		builder.Append(' ');
		builder.Append(string.IsNullOrEmpty(enPassantTarget) ? "-" : enPassantTarget);
		builder.Append(' ');
		builder.Append(halfmoveClock);
		builder.Append(' ');
		builder.Append(fullmoveNumber);

		return builder.ToString();
	}

	private static int CultureInvariantLength(
		char[] board,
		string castlingRights,
		string enPassantTarget,
		int    halfmoveClock,
		int    fullmoveNumber) =>
		BuildRawFen(board, 'w', castlingRights, enPassantTarget, halfmoveClock, fullmoveNumber).Length;

	private static LocalFenState ParseState(Fen fen)
	{
		var board = new char[64];
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
			NormalizeEnPassantTarget(fen.EnPassantTarget),
			fen.HalfmoveClock,
			fen.FullmoveNumber
		);
	}

	private static bool IsKingInCheck(char[] board, char color)
	{
		char king = color == 'w' ? 'K' : 'k';
		int kingIndex = Array.IndexOf(board, king);
		return kingIndex >= 0 && IsSquareAttacked(board, kingIndex, Opposite(color));
	}

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

	private static string RemoveCastlingRights(string castlingRights, string rightsToRemove)
	{
		if (string.IsNullOrEmpty(castlingRights))
			return string.Empty;

		string updated = castlingRights;
		foreach (char right in rightsToRemove)
			updated = updated.Replace(right.ToString(), string.Empty, StringComparison.Ordinal);

		return updated;
	}

	private static void MoveRookForCastling(char[] board, int kingDestinationIndex, bool kingside)
	{
		int rank = GetRank(kingDestinationIndex);
		int rookFromIndex = kingside ? GetIndex(7, rank) : GetIndex(0, rank);
		int rookToIndex = kingside ? GetIndex(5, rank) : GetIndex(3, rank);
		board[rookToIndex] = board[rookFromIndex];
		board[rookFromIndex] = '\0';
	}

	private static char ColorizePromotionPiece(char promotionPiece, char color)
	{
		char normalizedPiece = char.ToLowerInvariant(promotionPiece);
		return color == 'w' ? char.ToUpperInvariant(normalizedPiece) : normalizedPiece;
	}

	private static bool IsFriendlyPiece(char piece, char color) => piece != '\0' && GetPieceColor(piece) == color;

	private static bool IsEnemyPiece(char piece, char color) => piece != '\0' && GetPieceColor(piece) != color;

	private static char GetPieceColor(char piece) => char.IsUpper(piece) ? 'w' : 'b';

	private static char Opposite(char color) => color == 'w' ? 'b' : 'w';

	private static bool IsLightSquare(int index) => ((GetFile(index) + GetRank(index)) & 1) == 0;

	private static bool IsOnBoard(int file, int rank) => file is >= 0 and < 8 && rank is >= 0 and < 8;

	private static int GetIndex(int file, int rank) => rank * 8 + file;

	private static int GetFile(int index) => index % 8;

	private static int GetRank(int index) => index / 8;

	private static int ParseSquare(ReadOnlySpan<char> square) => GetIndex(square[0] - 'a', square[1] - '1');

	private static string FormatMove(int fromIndex, int toIndex, char promotionPiece = '\0')
	{
		string move = $"{FormatSquare(fromIndex)}{FormatSquare(toIndex)}";
		return promotionPiece == '\0' ? move : $"{move}{promotionPiece}";
	}

	private static string FormatSquare(int index) => $"{(char)('a' + GetFile(index))}{(char)('1' + GetRank(index))}";

	private static string NormalizeCastlingRights(string castlingRights) =>
		string.Equals(castlingRights, "-", StringComparison.Ordinal) ? string.Empty : castlingRights;

	private static string NormalizeEnPassantTarget(string enPassantTarget) =>
		string.Equals(enPassantTarget, "-", StringComparison.Ordinal)
			? string.Empty
			: enPassantTarget.ToLowerInvariant();

	private readonly record struct LocalFenState(
		char[] Board,
		char   ActiveColor,
		string CastlingRights,
		string EnPassantTarget,
		int    HalfmoveClock,
		int    FullmoveNumber
	);
}
