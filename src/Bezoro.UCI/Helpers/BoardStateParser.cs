using System.Collections.Generic;

internal static class BoardStateParser
{
	private const char   FirstFileChar        = 'a';
	private const char   RankSeparator        = '/';
	private const int    InitialFile          = 0;
	private const int    InitialRank          = 8;
	private const string EnPassantPlaceholder = "-";

	/// <summary>
	///     Parses a full FEN string to create a comprehensive board state.
	/// </summary>
	public static BoardState ParseFen(string fen)
	{
		string[] parts = fen.Split(' ');
		// Part 1: Piece Placement
		var                      fenParser = new FenParser();
		Dictionary<string, char> positions = fenParser.ParsePiecePlacement(parts[0]);
		// Part 2: Active Color
		char activeColor = parts[1][0];
		// Part 3: Castling Rights
		string castlingRights = parts[2];
		// Part 4: En Passant Target
		string? enPassantTarget = parts[3] == EnPassantPlaceholder ? null : parts[3];
		// Part 5: Halfmove Clock
		int halfmoveClock = int.Parse(parts[4]);
		// Part 6: Fullmove Number
		int fullmoveNumber = int.Parse(parts[5]);
		return new BoardState(positions, activeColor, castlingRights, enPassantTarget, halfmoveClock, fullmoveNumber);
	}

	private static string ToAlgebraic(int file, int rank) => $"{(char)(FirstFileChar + file)}{rank}";

	private sealed class FenParser
	{
		private readonly Dictionary<string, char> _positions = new();
		private          int                      _file      = InitialFile;
		private          int                      _rank      = InitialRank;

		public Dictionary<string, char> ParsePiecePlacement(string piecePlacement)
		{
			foreach (char symbol in piecePlacement)
			{
				switch (symbol)
				{
					case RankSeparator:
						AdvanceToNextRank();
						break;
					case >= '1' and <= '8':
						SkipFiles(symbol);
						break;
					default:
						PlacePiece(symbol);
						break;
				}
			}

			return _positions;
		}

		private void AdvanceToNextRank()
		{
			_rank--;
			_file = InitialFile;
		}

		private void PlacePiece(char pieceSymbol)
		{
			_positions.Add(ToAlgebraic(_file, _rank), pieceSymbol);
			_file++;
		}

		private void SkipFiles(char digit)
		{
			_file += (int)char.GetNumericValue(digit);
		}
	}
}

/// <summary>
///     Represents the complete state of a chessboard, parsed from a FEN string.
/// </summary>
internal sealed record BoardState(
	Dictionary<string, char> PiecePositions,
	char ActiveColor,
	string CastlingRights,
	string? EnPassantTarget,
	int HalfmoveClock,
	int FullmoveNumber
);
