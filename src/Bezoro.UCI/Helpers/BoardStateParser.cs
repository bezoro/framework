using System.Collections.Generic;

namespace Bezoro.UCI.Internals
{
	/// <summary>
	///     Helper class for parsing FEN strings and managing board state information.
	/// </summary>
	internal static class BoardStateParser
	{
		/// <summary>
		///     Parses a FEN string to extract the piece positions and the en passant target square.
		/// </summary>
		public static BoardState ParseFen(string fen)
		{
			string[]? parts           = fen.Split(' ');
			string    piecePlacement  = parts[0];
			string?   enPassantTarget = parts.Length > 3 && parts[3] != "-" ? parts[3] : null;

			var positions = new Dictionary<string, char>();
			var rank      = 8;
			var file      = 0; // 'a' is 0

			foreach (char c in piecePlacement)
			{
				if (c == '/')
				{
					rank--;
					file = 0;
				}
				else if (char.IsDigit(c))
				{
					file += (int)char.GetNumericValue(c);
				}
				else
				{
					var square = $"{(char)('a' + file)}{rank}";
					positions.Add(square, c);
					file++;
				}
			}

			return new BoardState(positions, enPassantTarget);
		}
	}

	/// <summary>
	///     A simple record to hold the essential parts of a board state parsed from a FEN string.
	/// </summary>
	internal sealed record BoardState(Dictionary<string, char> PiecePositions, string? EnPassantTarget);
}
