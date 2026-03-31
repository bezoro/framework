namespace Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

/// <summary>
///     Extension methods for compact debug display of move evaluations.
/// </summary>
public static class MoveEvaluationExtensions
{
	/// <summary>
	///     Returns a compact debug string combining the move, score, and classification suffix.
	/// </summary>
	/// <param name="evaluation">Move evaluation to format.</param>
	/// <returns>Formatted debug display string.</returns>
	public static string ToDebugDisplayString(this MoveEvaluation evaluation) =>
		$"{evaluation.Move} {evaluation.Display}{evaluation.Classification.ToDebugSuffix()}";
}
