namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>An immutable position evaluation from side-to-move perspective.</summary>
/// <param name="Position">Complete FEN at this point in the played game.</param>
/// <param name="Score">Engine score from the position's side-to-move perspective.</param>
/// <param name="BestMove">Preferred legal move, or an empty string for a terminal position.</param>
/// <param name="PrincipalVariation">Primary search line, absent for locally adjudicated terminal positions.</param>
/// <param name="Result">Local automatic terminal result, or the default value while play can continue.</param>
public readonly record struct GameReviewEvaluation(
	Fen Position,
	PositionScore Score,
	string BestMove,
	PrincipalVariation? PrincipalVariation,
	PlayableMatchResult Result)
{
	/// <summary>Gets whether local rules adjudicated a terminal position without an engine search.</summary>
	public bool IsTerminal => Result.IsTerminal;
}

