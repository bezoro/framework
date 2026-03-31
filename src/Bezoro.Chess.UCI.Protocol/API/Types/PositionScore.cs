using System.Globalization;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a position score normalized to the player's perspective.
/// </summary>
/// <param name="Cp">Centipawn score relative to the player, or <see langword="null" /> when mate is available.</param>
/// <param name="Mate">Mate score relative to the player, or <see langword="null" /> when centipawns are available.</param>
public readonly record struct PositionScore(int? Cp, int? Mate)
{
	/// <summary>
	///     Creates a player-relative score from raw engine output.
	/// </summary>
	/// <param name="rawCpScore">Engine centipawn score, from side-to-move perspective.</param>
	/// <param name="rawMateScore">Engine mate score, from side-to-move perspective.</param>
	/// <param name="sideToMove">Side to move for the evaluated position: <c>w</c> or <c>b</c>.</param>
	/// <param name="playerColor">Player side: <c>w</c> or <c>b</c>.</param>
	/// <returns>Normalized position score for the player.</returns>
	public static PositionScore FromEngineScore(
		int? rawCpScore,
		int? rawMateScore,
		char sideToMove,
		char playerColor)
	{
		int perspective = sideToMove == playerColor ? 1 : -1;

		if (rawMateScore is int mateScore)
			return new(null, mateScore * perspective);

		return new((rawCpScore ?? 0) * perspective, null);
	}

	/// <summary>
	///     Returns a descending-sort-friendly score where winning mates outrank centipawn evaluations.
	/// </summary>
	public double ToSortValue()
	{
		if (Mate is int mate)
			return mate > 0 ? 100_000 - Math.Abs(mate) : -100_000 + Math.Abs(mate);

		return Cp ?? 0;
	}

	/// <summary>
	///     Returns a compact display string for the score.
	/// </summary>
	public string ToDisplayString()
	{
		if (Mate is int mate)
			return mate > 0 ? $"+M{Math.Abs(mate)}" : $"-M{Math.Abs(mate)}";

		int cp = Cp ?? 0;
		return $"{(cp >= 0 ? "+" : string.Empty)}{cp.ToString(CultureInfo.InvariantCulture)} cp";
	}
}
