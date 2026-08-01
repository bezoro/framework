using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Bezoro.Chess.UCI.Protocol.Internal;

internal static partial class LocalPositionRules
{
	internal static Fen ApplyMove(Fen fen, string move)
	{
		string normalizedMove = NormalizeMove(move);
		var state = Parse(fen);
		if (!GenerateLegalMoves(state).Contains(normalizedMove, StringComparer.Ordinal))
			throw new InvalidOperationException("The move is not legal in the supplied FEN position.");

		var structural = ClassifyNormalized(state, normalizedMove);
		var next = ApplyMove(state, normalizedMove, structural);
		return Fen.Parse(BuildRawFen(next))!.Value;
	}

	internal static ImmutableArray<string> GetLegalMoves(Fen fen) => GenerateLegalMoves(Parse(fen));

	internal static bool IsCurrentPlayerInCheck(Fen fen)
	{
		var state = Parse(fen);
		return IsKingInCheck(state.Board, state.ActiveColor);
	}

	internal static bool TryCreatePendingPromotion(
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

		var state = Parse(fen);
		int fromIndex = ParseSquare(normalizedMove.AsSpan(0, 2));
		char movingPiece = state.Board[fromIndex];

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

	internal static bool HasInsufficientMaterial(Fen fen)
	{
		var state = Parse(fen);
		var knightCount = 0;
		bool? bishopIsLightSquare = null;

		for (var squareIndex = 0; squareIndex < state.Board.Length; squareIndex++)
		{
			char piece = state.Board[squareIndex];
			if (piece == '\0' || char.ToLowerInvariant(piece) == 'k')
				continue;

			switch (char.ToLowerInvariant(piece))
			{
				case 'b':
					bool isLightSquare = IsLightSquare(squareIndex);
					if (bishopIsLightSquare.HasValue && bishopIsLightSquare.Value != isLightSquare)
						return false;

					bishopIsLightSquare = isLightSquare;
					break;
				case 'n':
					knightCount++;
					break;
				default:
					return false;
			}
		}

		return bishopIsLightSquare.HasValue
			? knightCount == 0
			: knightCount <= 1;
	}

	internal static string BuildRepetitionKey(Fen fen) =>
		$"{fen.PiecePlacement} {fen.ActiveColor} {NormalizeCastlingRights(fen.CastlingRights)} {NormalizeEnPassantTarget(fen.EnPassantTarget)}";

	internal static ImmutableDictionary<string, MoveClassification> ClassifyMoves(
		Fen                 fen,
		IEnumerable<string> legalMoves)
	{
		if (legalMoves is null) throw new ArgumentNullException(nameof(legalMoves));

		var builder = ImmutableDictionary.CreateBuilder<string, MoveClassification>(StringComparer.Ordinal);
		LocalPositionState? state = null;
		foreach (string move in legalMoves)
		{
			string normalizedMove = NormalizeMove(move);
			state ??= Parse(fen);
			builder[move] = ClassifyNormalized(state.Value, normalizedMove);
		}

		return builder.ToImmutable();
	}

	internal static ImmutableDictionary<string, MoveClassification> ClassifyMovesFully(
		Fen                 fen,
		IEnumerable<string> legalMoves)
	{
		if (legalMoves is null) throw new ArgumentNullException(nameof(legalMoves));

		var builder = ImmutableDictionary.CreateBuilder<string, MoveClassification>(StringComparer.Ordinal);
		LocalPositionState? state = null;
		foreach (string move in legalMoves)
		{
			string normalizedMove = NormalizeMove(move);
			state ??= Parse(fen);
			var structural = ClassifyNormalized(state.Value, normalizedMove);
			builder[move] = LocalMoveTacticsResolver.Resolve(state.Value, normalizedMove, structural);
		}

		return builder.ToImmutable();
	}

	internal static MoveClassification ClassifyMove(Fen fen, string move)
	{
		string normalizedMove = NormalizeMove(move);
		return ClassifyNormalized(Parse(fen), normalizedMove);
	}

	internal static MoveClassification ClassifyMoveFully(Fen fen, string move)
	{
		string normalizedMove = NormalizeMove(move);
		var state = Parse(fen);
		var structural = ClassifyNormalized(state, normalizedMove);
		return LocalMoveTacticsResolver.Resolve(state, normalizedMove, structural);
	}

	internal static LocalPositionState ApplyMove(
		LocalPositionState state,
		string             move,
		MoveClassification structural)
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
			: string.Empty;

		int nextHalfmoveClock = char.ToLowerInvariant(movingPiece) == 'p' || structural.IsCapture
			? 0
			: state.HalfmoveClock + 1;

		int nextFullmoveNumber = state.ActiveColor == 'b'
			? state.FullmoveNumber + 1
			: state.FullmoveNumber;

		return new(
			board,
			Opposite(state.ActiveColor),
			castlingRights,
			enPassantTarget,
			nextHalfmoveClock,
			nextFullmoveNumber
		);
	}

	private static MoveClassification ClassifyNormalized(LocalPositionState state, string move)
	{
		string from = move[..2];
		string to = move.Substring(2, 2);
		int fromIndex = ParseSquare(move.AsSpan(0, 2));
		int toIndex = ParseSquare(move.AsSpan(2, 2));
		char movingPiece = state.Board[fromIndex];
		if (movingPiece == '\0')
			throw new ArgumentException($"No piece exists on source square '{from}'.", nameof(move));

		char target = state.Board[toIndex];
		char? targetPiece = target == '\0' ? null : target;
		bool isPawn = char.ToLowerInvariant(movingPiece) == 'p';
		bool isPromotion = move.Length == 5;
		char? promotionPiece = isPromotion ? move[4] : null;
		bool isEnPassant = isPawn &&
			from[0] != to[0] &&
			targetPiece is null &&
			string.Equals(state.EnPassantTarget, to, StringComparison.Ordinal);

		char? capturedPiece = targetPiece;
		if (isEnPassant)
		{
			int capturedPawnIndex = state.ActiveColor == 'w' ? toIndex - 8 : toIndex + 8;
			char captured = state.Board[capturedPawnIndex];
			capturedPiece = captured == '\0' ? null : captured;
		}

		var flags = MoveClassificationFlags.None;
		if (isPromotion)
			flags |= MoveClassificationFlags.Promotion;

		if (capturedPiece.HasValue)
			flags |= MoveClassificationFlags.Capture;

		if (isEnPassant)
			flags |= MoveClassificationFlags.EnPassant;

		if (IsKingsideCastlingMove(movingPiece, from, to))
			flags |= MoveClassificationFlags.KingsideCastling;
		else if (IsQueensideCastlingMove(movingPiece, from, to))
			flags |= MoveClassificationFlags.QueensideCastling;

		if (IsDoublePawnPush(movingPiece, from, to))
			flags |= MoveClassificationFlags.DoublePawnPush;

		if ((flags &
			 (MoveClassificationFlags.Capture |
			  MoveClassificationFlags.EnPassant |
			  MoveClassificationFlags.Promotion |
			  MoveClassificationFlags.KingsideCastling |
			  MoveClassificationFlags.QueensideCastling)) ==
			0)
			flags |= MoveClassificationFlags.Normal;

		return MoveClassification.CreateStructural(flags, movingPiece, capturedPiece, promotionPiece);
	}

	private static bool IsDoublePawnPush(char movingPiece, string from, string to) =>
		char.ToLowerInvariant(movingPiece) == 'p' &&
		from[0] == to[0] &&
		Math.Abs(to[1] - '0' - (from[1] - '0')) == 2;

	private static bool IsKingsideCastlingMove(char movingPiece, string from, string to) =>
		char.ToLowerInvariant(movingPiece) == 'k' && from[0] == 'e' && to[0] == 'g';

	private static bool IsQueensideCastlingMove(char movingPiece, string from, string to) =>
		char.ToLowerInvariant(movingPiece) == 'k' && from[0] == 'e' && to[0] == 'c';

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

	internal static void MoveRookForCastling(char[] board, int kingDestinationIndex, bool kingside)
	{
		int rank = GetRank(kingDestinationIndex);
		int rookFromIndex = kingside ? GetIndex(7, rank) : GetIndex(0, rank);
		int rookToIndex = kingside ? GetIndex(5, rank) : GetIndex(3, rank);
		board[rookToIndex] = board[rookFromIndex];
		board[rookFromIndex] = '\0';
	}

	internal static char ColorizePromotionPiece(char promotionPiece, char color)
	{
		char normalizedPiece = char.ToLowerInvariant(promotionPiece);
		return color == 'w' ? char.ToUpperInvariant(normalizedPiece) : normalizedPiece;
	}

	internal static char Opposite(char color) => color == 'w' ? 'b' : 'w';

	private static bool IsLightSquare(int index) => ((GetFile(index) + GetRank(index)) & 1) == 0;
}
