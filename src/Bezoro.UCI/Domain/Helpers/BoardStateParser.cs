using System.Collections.Generic;
using System.Linq;

namespace Bezoro.UCI.Domain.Helpers;

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

	/// <summary>
	///     Validates if a string is a well-formed Forsyth-Edwards Notation (FEN) string.
	/// </summary>
	/// <param name="FEN">The FEN string to validate.</param>
	/// <returns>True if the FEN string is structurally valid, otherwise false.</returns>
	public static bool IsValidFEN(string fen)
	{
		if (string.IsNullOrWhiteSpace(fen))
		{
			return false;
		}

		string[] parts = fen.Split(' ');
		if (parts.Length != 6)
		{
			return false;
		}

		return ValidatePiecePlacement(parts[0])                      &&
			   ValidateActiveColor(Convert.ToChar(parts[1]))         &&
			   ValidateCastling(parts[2])                            &&
			   ValidateEnPassant(parts[3], Convert.ToChar(parts[1])) &&
			   ValidateHalfmoveClock(parts[4])                       &&
			   ValidateFullmoveNumber(parts[5]);
	}

	public static bool ValidateActiveColor(char activeColor) =>
		activeColor is 'w' or 'b';

	private static bool ValidateCastling(string castling)
	{
		if (castling == "-")
		{
			return true;
		}

		const string validCastlingChars = "KQkq";
		// No invalid characters, no duplicates, and not too long
		return castling.Length <= 4                      &&
			   castling.All(validCastlingChars.Contains) &&
			   castling.Distinct().Count() == castling.Length;
	}

	public static bool ValidateEnPassant(string enPassant, char activeColor)
	{
		if (enPassant == "-")
		{
			return true;
		}

		if (enPassant.Length != 2)
		{
			return false;
		}

		char file = enPassant[0];
		char rank = enPassant[1];

		if (file is < 'a' or > 'h')
		{
			return false;
		}

		// En passant rank must be 6 for white to move, or 3 for black to move.
		return activeColor == 'w' && rank == '6' || activeColor == 'b' && rank == '3';
	}

	public static bool ValidateFullmoveNumber(string fullmoveNumber) =>
		int.TryParse(fullmoveNumber, out int number) && number >= 1;

	public static bool ValidateHalfmoveClock(string halfmoveClock) =>
		int.TryParse(halfmoveClock, out int clock) && clock >= 0;

	public static bool ValidatePiecePlacement(string piecePlacement)
	{
		string[] ranks = piecePlacement.Split('/');
		if (ranks.Length != 8 || !ranks.All(ValidateRank))
		{
			return false;
		}

		// A valid board must have exactly one white and one black king.
		return piecePlacement.Count(c => c == 'K') == 1 &&
			   piecePlacement.Count(c => c == 'k') == 1;
	}

	private static bool ValidateRank(string rank)
	{
		if (string.IsNullOrEmpty(rank))
		{
			return false;
		}

		const string validPieces = "pnbrqkPNBRQK";
		var          fileCount   = 0;
		foreach (char c in rank)
		{
			if (char.IsDigit(c))
			{
				if (c is >= '1' and <= '8')
				{
					fileCount += (int)char.GetNumericValue(c);
				}
				else
				{
					return false; // Invalid digit
				}
			}
			else if (validPieces.Contains(c))
			{
				fileCount++;
			}
			else
			{
				return false; // Invalid character
			}
		}

		return fileCount == 8; // Rank must total 8 squares
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
