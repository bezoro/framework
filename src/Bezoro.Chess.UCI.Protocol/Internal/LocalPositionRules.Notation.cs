using System.Text;

namespace Bezoro.Chess.UCI.Protocol.Internal;

internal static partial class LocalPositionRules
{
	internal static LocalPositionState Parse(Fen fen)
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

	internal static string NormalizeMove(string move)
	{
		if (string.IsNullOrWhiteSpace(move))
			throw new ArgumentException("Move must not be blank.", nameof(move));

		string normalizedMove = move.Trim().ToLowerInvariant();
		if (!UciEngineClient.IsUciMoveString(normalizedMove))
			throw new ArgumentException("Move must be valid UCI notation.", nameof(move));

		return normalizedMove;
	}

	internal static int ParseSquare(ReadOnlySpan<char> square) => GetIndex(square[0] - 'a', square[1] - '1');

	internal static string FormatMove(int fromIndex, int toIndex, char promotionPiece = '\0')
	{
		string move = $"{FormatSquare(fromIndex)}{FormatSquare(toIndex)}";
		return promotionPiece == '\0' ? move : $"{move}{promotionPiece}";
	}

	internal static string FormatSquare(int index) =>
		$"{(char)('a' + GetFile(index))}{(char)('1' + GetRank(index))}";

	internal static int GetIndex(int file, int rank) => rank * 8 + file;

	internal static int GetFile(int index) => index % 8;

	internal static int GetRank(int index) => index / 8;

	internal static bool IsOnBoard(int file, int rank) => file is >= 0 and < 8 && rank is >= 0 and < 8;

	internal static string NormalizeCastlingRights(string castlingRights) =>
		string.Equals(castlingRights, "-", StringComparison.Ordinal) ? string.Empty : castlingRights;

	internal static string NormalizeEnPassantTarget(string enPassantTarget) =>
		string.Equals(enPassantTarget, "-", StringComparison.Ordinal)
			? string.Empty
			: enPassantTarget.ToLowerInvariant();

	private static string BuildRawFen(LocalPositionState state)
	{
		var builder = new StringBuilder(96);
		for (var rank = 7; rank >= 0; rank--)
		{
			var emptyCount = 0;
			for (var file = 0; file < 8; file++)
			{
				char piece = state.Board[GetIndex(file, rank)];
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
		builder.Append(state.ActiveColor);
		builder.Append(' ');
		builder.Append(string.IsNullOrEmpty(state.CastlingRights) ? "-" : state.CastlingRights);
		builder.Append(' ');
		builder.Append(string.IsNullOrEmpty(state.EnPassantTarget) ? "-" : state.EnPassantTarget);
		builder.Append(' ');
		builder.Append(state.HalfmoveClock);
		builder.Append(' ');
		builder.Append(state.FullmoveNumber);

		return builder.ToString();
	}
}
