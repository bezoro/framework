using System.Collections.Generic;

namespace Bezoro.UCI.Helpers
{
	/// <summary>
	///     Helper class for parsing FEN strings and managing board state information.
	/// </summary>
	internal static class BoardStateParser
	{
		private const char   FirstFileChar        = 'a';
		private const char   RankSeparator        = '/';
		private const int    InitialFile          = 0;
		private const int    InitialRank          = 8;
		private const string EnPassantPlaceholder = "-";

		/// <summary>
		///     Parses a FEN string to extract the piece positions and the en passant target square.
		/// </summary>
		public static BoardState ParseFen(string fen)
		{
			string[]? parts           = fen.Split(' ');
			string    piecePlacement  = parts[0];
			string?   enPassantTarget = parts.Length > 3 && parts[3] != EnPassantPlaceholder ? parts[3] : null;

			var positions = new Dictionary<string, char>();
			int rank      = InitialRank;
			int file      = InitialFile;

			foreach (char symbol in piecePlacement)
			{
				if (symbol == RankSeparator)
				{
					AdvanceToNextRank();
				}
				else if (char.IsDigit(symbol))
				{
					SkipFiles(symbol);
				}
				else
				{
					PlacePiece(symbol);
				}
			}

			return new BoardState(positions, enPassantTarget);

			void AdvanceToNextRank()
			{
				rank--;
				file = InitialFile;
			}

			void SkipFiles(char digit)
			{
				file += (int)char.GetNumericValue(digit);
			}

			void PlacePiece(char pieceSymbol)
			{
				positions.Add(ToAlgebraic(file, rank), pieceSymbol);
				file++;
			}
		}

		private static string ToAlgebraic(int file, int rank) => $"{(char)(FirstFileChar + file)}{rank}";
	}

	/// <summary>
	///     A simple record to hold the essential parts of a board state parsed from a FEN string.
	/// </summary>
	internal sealed record BoardState(Dictionary<string, char> PiecePositions, string? EnPassantTarget);
}
