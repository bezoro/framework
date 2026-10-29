namespace Bezoro.Chess.UCI.Protocol.API.Abstractions;

/// <summary>Converts position evidence into expected points using an application's chosen probability model.</summary>
public interface IExpectedPointsModel
{
	/// <summary>Gets a stable, descriptive model name recorded in review results.</summary>
	string Name { get; }

	/// <summary>Returns expected points in [0,1] for the requested player.</summary>
	/// <param name="evaluation">Position and engine evidence from its side-to-move perspective.</param>
	/// <param name="playerSide">Player whose expected points are requested.</param>
	/// <param name="playerRating">Optional rating supplied by the application.</param>
	/// <returns>Expected win points plus half the expected draw points.</returns>
	double GetExpectedPoints(GameReviewEvaluation evaluation, char playerSide, int? playerRating);
}

