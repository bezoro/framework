namespace Bezoro.Chess.UCI.Protocol.API;

/// <summary>Maps expected-points loss to ordinary move-quality categories with explicit interval boundaries.</summary>
public static class MoveQualityClassifier
{
	/// <summary>Classifies a loss using 0, 0.02, 0.05, 0.10, and 0.20 thresholds.</summary>
	/// <param name="expectedPointsLoss">Finite loss in [0,1].</param>
	/// <returns>Best at zero; each nonzero threshold belongs to the worse category.</returns>
	public static MoveQuality Classify(double expectedPointsLoss)
	{
		if (double.IsNaN(expectedPointsLoss) || expectedPointsLoss < 0 || expectedPointsLoss > 1)
			throw new ArgumentOutOfRangeException(nameof(expectedPointsLoss));
		return expectedPointsLoss switch
		{
			0 => MoveQuality.Best,
			< 0.02 => MoveQuality.Excellent,
			< 0.05 => MoveQuality.Good,
			< 0.10 => MoveQuality.Inaccuracy,
			< 0.20 => MoveQuality.Mistake,
			_ => MoveQuality.Blunder
		};
	}
}
