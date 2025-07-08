using System.Text;
using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.Domain.Helpers;

namespace Bezoro.UCI.API;

public static class FENHelpers
{
	/// <summary>
	///     Derives the castling availability string from the piece placement part of a FEN.
	///     This method checks if the kings and rooks are on their starting squares.
	/// </summary>
	/// <param name="piecePlacement">The FEN piece placement string (e.g., "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR").</param>
	/// <returns>The castling availability string (e.g., "KQkq", "-", "Kq").</returns>
	public static string DeriveCastlingRightsFromPiecePlacementFEN(string piecePlacement)
	{
		string[]? ranks = piecePlacement.Split('/');
		if (ranks.Length != 8)
		{
			// Return no rights for invalid piece placement string
			return "-";
		}

		// Create an 8x8 board representation for easier lookup
		var board = new char[8, 8]; // [rank, file]
		for (var i = 0 ; i < 8 ; i++)
		{
			var fileIndex = 0;
			foreach (char c in ranks[i]) // ranks[0] is rank 8, ranks[7] is rank 1
			{
				if (fileIndex >= 8)
				{
					break;
				}

				if (char.IsDigit(c))
				{
					fileIndex += (int)char.GetNumericValue(c);
				}
				else
				{
					board[i, fileIndex] = c;
					fileIndex++;
				}
			}
		}

		var castlingRights = new StringBuilder();

		// Check White's castling rights (King on e1)
		if (board[7, 4] == 'K')
		{
			if (board[7, 7] == 'R')
			{
				castlingRights.Append('K'); // Rook on h1
			}

			if (board[7, 0] == 'R')
			{
				castlingRights.Append('Q'); // Rook on a1
			}
		}

		// Check Black's castling rights (King on e8)
		if (board[0, 4] == 'k')
		{
			if (board[0, 7] == 'r')
			{
				castlingRights.Append('k'); // Rook on h8
			}

			if (board[0, 0] == 'r')
			{
				castlingRights.Append('q'); // Rook on a8
			}
		}

		return castlingRights.Length == 0 ? "-" : castlingRights.ToString();
	}

	/// <summary>
	///     Constructs a full FEN string by deriving castling rights from the piece placement.
	/// </summary>
	/// <param name="piecePlacement">The FEN piece placement string (e.g., "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR").</param>
	/// <param name="activeColor">The color of the player to move ('w' or 'b').</param>
	/// <param name="enPassantTarget">The en passant target square in algebraic notation (e.g., "e3"), or "-" if none.</param>
	/// <param name="halfmoveClock">The number of half-moves since the last capture or pawn advance.</param>
	/// <param name="fullmoveNumber">The number of the full move. It starts at 1 and is incremented after Black's move.</param>
	/// <returns>A complete and valid FEN string.</returns>
	public static string BuildFENFromParts(
		string piecePlacement, char activeColor, string enPassantTarget = "-",
		int halfmoveClock = 0, int fullmoveNumber = 1)
	{
		Logger.LogInfo($"Generating FEN from pieces...", nameof(FENHelpers), LogCategory.UCI);
		BoardStateParser.ValidatePiecePlacement(piecePlacement);
		BoardStateParser.ValidateActiveColor(activeColor);
		BoardStateParser.ValidateEnPassant(enPassantTarget, activeColor);
		BoardStateParser.ValidateHalfmoveClock(halfmoveClock.ToString());
		BoardStateParser.ValidateFullmoveNumber(fullmoveNumber.ToString());

		string castlingRights = DeriveCastlingRightsFromPiecePlacementFEN(piecePlacement);
		var fen = $"{piecePlacement} {activeColor} {castlingRights} {enPassantTarget} {halfmoveClock} {fullmoveNumber}";
		Logger.LogSuccess($"FEN Generated: {fen.Bold()}", nameof(FENHelpers), LogCategory.UCI);
		return fen;
	}
}
