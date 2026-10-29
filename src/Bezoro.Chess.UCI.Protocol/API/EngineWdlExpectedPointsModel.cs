namespace Bezoro.Chess.UCI.Protocol.API;

/// <summary>Uses engine-reported WDL self-play probabilities. It does not estimate human winning chances or use player ratings.</summary>
public sealed class EngineWdlExpectedPointsModel : IExpectedPointsModel
{
	/// <summary>Gets a reusable stateless model instance.</summary>
	public static EngineWdlExpectedPointsModel Instance { get; } = new();

	/// <inheritdoc />
	public string Name => "Engine WDL self-play";

	/// <inheritdoc />
	public double GetExpectedPoints(GameReviewEvaluation evaluation, char playerSide, int? playerRating)
	{
		if (playerSide is not ('w' or 'b'))
			throw new ArgumentOutOfRangeException(nameof(playerSide));
		if (evaluation.IsTerminal)
			return evaluation.Result.IsDraw ? 0.5 : evaluation.Result.Winner == playerSide ? 1 : 0;

		double expected;
		if (evaluation.PrincipalVariation?.Wdl is { } wdl)
			expected = wdl.ExpectedPoints;
		else if (evaluation.Score.Mate is int mate && mate != 0)
			expected = mate > 0 ? 1 : 0;
		else
			throw new NotSupportedException("The engine did not report WDL probabilities. Enable UCI_ShowWDL or provide an IExpectedPointsModel for this engine.");
		return evaluation.Position.ActiveColor == playerSide ? expected : 1 - expected;
	}
}
