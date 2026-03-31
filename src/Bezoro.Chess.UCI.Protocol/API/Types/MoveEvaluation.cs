namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a scored legal move candidate from the player's perspective.
/// </summary>
/// <param name="Move">Move in UCI notation.</param>
/// <param name="Score">Absolute player-relative score for the resulting position.</param>
/// <param name="Classification">Move classification metadata when available.</param>
public readonly record struct MoveEvaluation(string Move, PositionScore Score, MoveClassification Classification = default)
{
	/// <summary>
	///     Gets a compact player-relative display string for the resulting position score.
	/// </summary>
	public string Display => Score.ToDisplayString();

	/// <summary>
	///     Gets a descending-sort-friendly numeric value for the resulting position score.
	/// </summary>
	public double SortValue => Score.ToSortValue();
}
