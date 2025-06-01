using System;
using System.Linq;

namespace Bezoro.Core.Chess.Utils
{
	public static class FenUtility
	{
		private const string _STANDARD_BOARD_FEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

		public static FenData StandardBoard => ParseFen(_STANDARD_BOARD_FEN);

		public static FenData ParseFen(string fenString)
		{
			if (string.IsNullOrWhiteSpace(fenString))
			{
				throw new ArgumentException("FEN string cannot be null or empty.", nameof(fenString));
			}

			var fenParts = fenString.Split(' ');

			if (fenParts.Length != 6)
			{
				throw new ArgumentException("FEN string must have 6 parts separated by spaces.", nameof(fenString));
			}

			return new()
			{
				PiecePlacement        = fenParts[0],
				ActiveColor           = fenParts[1].FirstOrDefault(),
				CastlingAvailability  = fenParts[2],
				EnPassantTargetSquare = fenParts[3],
				HalfmoveClock         = int.TryParse(fenParts[4], out var halfmove) ? halfmove : 0,
				FullmoveNumber        = int.TryParse(fenParts[5], out var fullmove) ? fullmove : 1
			};
		}
	}

	public record FenData
	{
public char   ActiveColor           { get; init; } = 'w';
public int    FullmoveNumber        { get; init; } = 1;
public int    HalfmoveClock         { get; init; } = 0;
public string CastlingAvailability  { get; init; } = "KQkq";
public string EnPassantTargetSquare { get; init; } = "-";
public string PiecePlacement        { get; init; } = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR";
	}
}
