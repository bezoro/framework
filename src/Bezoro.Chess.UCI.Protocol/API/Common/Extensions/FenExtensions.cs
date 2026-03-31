using System.Collections.Generic;
using System.Linq;

namespace Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

/// <summary>
///     Extension methods for formatting <see cref="Fen" /> values for text UIs.
/// </summary>
public static class FenExtensions
{
	/// <summary>
	///     Builds display lines for a board view from the requested player's perspective.
	/// </summary>
	/// <param name="fen">Position to render.</param>
	/// <param name="playerColor">Perspective to render from: <c>w</c> or <c>b</c>.</param>
	/// <param name="legalMoveCount">Number of legal moves to include in the header.</param>
	/// <returns>Board display lines suitable for console output.</returns>
	public static string[] ToDisplayLines(this Fen fen, char playerColor, int legalMoveCount)
	{
		var lines = new List<string>
		{
			$"Move {fen.FullmoveNumber} | {(fen.ActiveColor == 'w' ? "White" : "Black")} to move | {legalMoveCount} legal moves"
		};

		if (!string.IsNullOrWhiteSpace(fen.Checkers))
			lines.Add("Check.");

		string[] ranks = fen.PiecePlacement.Split('/');
		if (ranks.Length != 8)
		{
			lines.Add($"FEN: {fen.Raw}");
			return lines.ToArray();
		}

		bool whitePerspective = playerColor == 'w';
		lines.Add(whitePerspective ? "  a b c d e f g h" : "  h g f e d c b a");

		if (whitePerspective)
		{
			for (var rankIndex = 0; rankIndex < ranks.Length; rankIndex++)
			{
				int rankLabel = 8 - rankIndex;
				string expandedRank = ExpandRank(ranks[rankIndex]);

				lines.Add($"{rankLabel} {string.Join(" ", expandedRank.ToCharArray())} {rankLabel}");
			}
		}
		else
		{
			for (int rankIndex = ranks.Length - 1; rankIndex >= 0; rankIndex--)
			{
				int rankLabel = 8 - rankIndex;
				string expandedRank = new string(ExpandRank(ranks[rankIndex]).Reverse().ToArray());

				lines.Add($"{rankLabel} {string.Join(" ", expandedRank.ToCharArray())} {rankLabel}");
			}
		}

		lines.Add(whitePerspective ? "  a b c d e f g h" : "  h g f e d c b a");
		return lines.ToArray();
	}

	private static string ExpandRank(string encodedRank)
	{
		var chars = new List<char>(8);

		foreach (char symbol in encodedRank)
		{
			if (char.IsDigit(symbol))
			{
				for (var i = 0; i < symbol - '0'; i++)
					chars.Add('.');

				continue;
			}

			chars.Add(symbol);
		}

		return new(chars.ToArray());
	}
}
