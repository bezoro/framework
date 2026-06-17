using System;
using System.Collections.Generic;
using Bezoro.Chess.UCI.Protocol.API.Types;

namespace Bezoro.Chess.UCI.Protocol.API.Common.Helpers;

/// <summary>
///     Parses the piece-placement field of Forsyth-Edwards Notation into occupied board squares.
/// </summary>
public static class FenPiecePlacementParser
{
	/// <summary>
	///     Parses piece placement from either a full FEN string or its first placement field.
	/// </summary>
	/// <param name="fenOrPlacement">Full FEN text or only the slash-separated placement field.</param>
	/// <param name="output">
	///     Dictionary that receives occupied squares and their FEN piece symbols. The dictionary is cleared before a
	///     successful copy and is also cleared when parsing fails.
	/// </param>
	/// <returns><see langword="true" /> when the placement is valid and was copied to <paramref name="output" />.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="output" /> is <see langword="null" />.</exception>
	public static bool TryParse(string? fenOrPlacement, IDictionary<ChessSquare, char> output)
	{
		if (output is null)
			throw new ArgumentNullException(nameof(output));

		if (string.IsNullOrWhiteSpace(fenOrPlacement))
			return ClearAndFail(output);

		string[] fenParts = fenOrPlacement.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (fenParts.Length == 0)
			return ClearAndFail(output);

		string[] ranks = fenParts[0].Split('/');
		if (ranks.Length != ChessSquare.BoardSize)
			return ClearAndFail(output);

		var parsedPieces = new Dictionary<ChessSquare, char>();
		for (var fenRankIndex = 0; fenRankIndex < ranks.Length; fenRankIndex++)
		{
			string rank = ranks[fenRankIndex];
			var fileIndex = 0;
			int boardRankIndex = ChessSquare.BoardSize - 1 - fenRankIndex;
			var previousWasDigit = false;

			foreach (char symbol in rank)
			{
				if (char.IsDigit(symbol))
				{
					if (previousWasDigit || symbol is < '1' or > '8')
						return ClearAndFail(output);

					fileIndex += symbol - '0';
					if (fileIndex > ChessSquare.BoardSize)
						return ClearAndFail(output);

					previousWasDigit = true;
					continue;
				}

				previousWasDigit = false;
				if (!IsPieceSymbol(symbol))
					return ClearAndFail(output);

				if (!ChessSquare.TryCreate(fileIndex, boardRankIndex, out var square))
					return ClearAndFail(output);

				parsedPieces[square] = symbol;
				fileIndex++;
			}

			if (fileIndex != ChessSquare.BoardSize)
				return ClearAndFail(output);
		}

		output.Clear();
		foreach (var (square, piece) in parsedPieces)
			output[square] = piece;

		return true;
	}

	private static bool ClearAndFail(IDictionary<ChessSquare, char> output)
	{
		output.Clear();
		return false;
	}

	private static bool IsPieceSymbol(char symbol)
	{
		return char.ToLowerInvariant(symbol) switch
		{
			'p' or 'n' or 'b' or 'r' or 'q' or 'k' => true,
			_ => false
		};
	}
}
