using System.Collections.Generic;
using System.Collections.Immutable;
using Bezoro.Chess.UCI.Protocol.Internal;

namespace Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

/// <summary>
///     Extension methods for deriving structural move classifications directly from a FEN and UCI move strings.
/// </summary>
public static class FenMoveClassificationExtensions
{
	/// <summary>
	///     Classifies a set of legal moves structurally without engine search.
	/// </summary>
	/// <param name="fen">Current position.</param>
	/// <param name="legalMoves">Legal moves in lowercase UCI notation.</param>
	/// <returns>Move classifications keyed by move notation.</returns>
	public static ImmutableDictionary<string, MoveClassification> ClassifyMoves(
		this Fen            fen,
		IEnumerable<string> legalMoves)
	{
		if (legalMoves is null) throw new ArgumentNullException(nameof(legalMoves));

		var builder = ImmutableDictionary.CreateBuilder<string, MoveClassification>(StringComparer.Ordinal);
		foreach (string move in legalMoves)
			builder[move] = fen.ClassifyMove(move);

		return builder.ToImmutable();
	}

	/// <summary>
	///     Classifies a set of legal moves structurally and resolves check, mate, and stalemate locally without engine search.
	/// </summary>
	/// <param name="fen">Current position.</param>
	/// <param name="legalMoves">Legal moves in lowercase UCI notation.</param>
	/// <returns>Fully resolved move classifications keyed by move notation.</returns>
	public static ImmutableDictionary<string, MoveClassification> ClassifyMovesFully(
		this Fen            fen,
		IEnumerable<string> legalMoves)
	{
		if (legalMoves is null) throw new ArgumentNullException(nameof(legalMoves));

		var builder = ImmutableDictionary.CreateBuilder<string, MoveClassification>(StringComparer.Ordinal);
		foreach (string move in legalMoves)
			builder[move] = fen.ClassifyMoveFully(move);

		return builder.ToImmutable();
	}

	/// <summary>
	///     Classifies a single legal move structurally without engine search.
	/// </summary>
	/// <param name="fen">Current position.</param>
	/// <param name="move">Move in lowercase UCI notation.</param>
	/// <returns>Structural move classification with unresolved tactical flags.</returns>
	public static MoveClassification ClassifyMove(this Fen fen, string move)
	{
		if (string.IsNullOrWhiteSpace(move))
			throw new ArgumentException("Move must not be blank.", nameof(move));

		string normalizedMove = move.Trim().ToLowerInvariant();
		if (!UciEngineClient.IsUciMoveString(normalizedMove))
			throw new ArgumentException("Move must be valid UCI notation.", nameof(move));

		var    board = ExpandBoard(fen.PiecePlacement);
		string from  = normalizedMove[..2];
		string to    = normalizedMove.Substring(2, 2);
		char movingPiece = GetPieceAt(board, from) ??
						   throw new ArgumentException($"No piece exists on source square '{from}'.", nameof(move));

		char? targetPiece    = GetPieceAt(board, to);
		bool  isPawn         = char.ToLowerInvariant(movingPiece) == 'p';
		bool  isPromotion    = normalizedMove.Length == 5;
		char? promotionPiece = isPromotion ? normalizedMove[4] : null;
		bool isEnPassant = isPawn &&
						   from[0] != to[0] &&
						   targetPiece is null &&
						   string.Equals(fen.EnPassantTarget, to, StringComparison.OrdinalIgnoreCase);

		char? capturedPiece = targetPiece;
		if (isEnPassant)
		{
			int targetRank     = to[1] - '0';
			int capturedRank   = fen.ActiveColor == 'w' ? targetRank - 1 : targetRank + 1;
			var capturedSquare = $"{to[0]}{capturedRank}";
			capturedPiece = GetPieceAt(board, capturedSquare);
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

	/// <summary>
	///     Classifies a single legal move structurally and resolves check, mate, and stalemate locally without engine search.
	/// </summary>
	/// <param name="fen">Current position.</param>
	/// <param name="move">Move in lowercase UCI notation.</param>
	/// <returns>Fully resolved move classification.</returns>
	public static MoveClassification ClassifyMoveFully(this Fen fen, string move)
	{
		var structural = fen.ClassifyMove(move);
		return LocalMoveTacticsResolver.Resolve(fen, move.Trim().ToLowerInvariant(), structural);
	}

	private static bool IsDoublePawnPush(char movingPiece, string from, string to) =>
		char.ToLowerInvariant(movingPiece) == 'p' &&
		from[0] == to[0] &&
		Math.Abs(to[1] - '0' - (from[1] - '0')) == 2;

	private static bool IsKingsideCastlingMove(char movingPiece, string from, string to) =>
		char.ToLowerInvariant(movingPiece) == 'k' && from[0] == 'e' && to[0] == 'g';

	private static bool IsQueensideCastlingMove(char movingPiece, string from, string to) =>
		char.ToLowerInvariant(movingPiece) == 'k' && from[0] == 'e' && to[0] == 'c';

	private static char? GetPieceAt(IReadOnlyDictionary<string, char> board, string square) =>
		board.TryGetValue(square, out char piece) ? piece : null;

	private static IReadOnlyDictionary<string, char> ExpandBoard(string piecePlacement)
	{
		var      board = new Dictionary<string, char>(64, StringComparer.Ordinal);
		string[] ranks = piecePlacement.Split('/');
		if (ranks.Length != 8)
			throw new ArgumentException("Piece placement must contain eight ranks.", nameof(piecePlacement));

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

				var square = $"{(char)('a' + fileIndex)}{8 - rankIndex}";
				board[square] = symbol;
				fileIndex++;
			}
		}

		return board;
	}
}
