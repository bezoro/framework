using System;
using Bezoro.Chess.API.Types;

namespace Bezoro.Chess.Internal;

/// <summary>
///     Calculates ELO rating changes after a game.
///     Uses the standard FIDE ELO calculation formula.
/// </summary>
public static class EloCalculator
{
	/// <summary>
	///     Default K-factor for rating calculations.
	///     FIDE uses 40 for new players, 20 for established, 10 for masters.
	/// </summary>
	public const int DefaultKFactor = 32;

	/// <summary>
	///     K-factor for established players (30+ games, rating < 2400).
	/// </summary>
	public const int EstablishedKFactor = 20;

	/// <summary>
	///     K-factor for master players (rating >= 2400).
	/// </summary>
	public const int MasterKFactor = 10;

	/// <summary>
	///     K-factor for new players (fewer than 30 games).
	/// </summary>
	public const int NewPlayerKFactor = 40;

	/// <summary>
	///     Calculates rating changes for both players after a game.
	/// </summary>
	/// <param name="player1">First player's profile.</param>
	/// <param name="player2">Second player's profile.</param>
	/// <param name="result">The game result.</param>
	/// <param name="player1Color">First player's color.</param>
	/// <returns>A tuple with (player1Change, player2Change).</returns>
	public static (int Player1Change, int Player2Change) ForBothPlayers(
		PlayerProfile player1,
		PlayerProfile player2,
		GameResult    result,
		PlayerColor   player1Color)
	{
		var player2Color = player1Color.Opponent();

		int k1 = GetKFactor(player1);
		int k2 = GetKFactor(player2);

		// Use average K-factor for fairness
		int kFactor = (k1 + k2) / 2;

		int player1Change = ForResult(player1.Elo, player2.Elo, result, player1Color, kFactor);
		int player2Change = ForResult(player2.Elo, player1.Elo, result, player2Color, kFactor);

		return (player1Change, player2Change);
	}

	/// <summary>
	///     Updates both player profiles after a game.
	/// </summary>
	/// <param name="player1">First player's profile.</param>
	/// <param name="player2">Second player's profile.</param>
	/// <param name="result">The game result.</param>
	/// <param name="player1Color">First player's color.</param>
	/// <returns>A tuple with updated profiles.</returns>
	public static (PlayerProfile Player1, PlayerProfile Player2) UpdateProfiles(
		PlayerProfile player1,
		PlayerProfile player2,
		GameResult    result,
		PlayerColor   player1Color)
	{
		(int change1, int change2) = ForBothPlayers(player1, player2, result, player1Color);
		var player2Color = player1Color.Opponent();

		PlayerProfile newProfile1;
		PlayerProfile newProfile2;

		if (result.IsDraw)
		{
			newProfile1 = player1.WithDraw(change1);
			newProfile2 = player2.WithDraw(change2);
		}
		else if (result.Winner.HasValue)
		{
			if (result.Winner.Value == player1Color)
			{
				newProfile1 = player1.WithWin(change1);
				newProfile2 = player2.WithLoss(change2);
			}
			else
			{
				newProfile1 = player1.WithLoss(change1);
				newProfile2 = player2.WithWin(change2);
			}
		}
		else
		{
			// Game not finished, no update
			newProfile1 = player1;
			newProfile2 = player2;
		}

		return (newProfile1, newProfile2);
	}

	/// <summary>
	///     Calculates the expected score for a player.
	/// </summary>
	/// <param name="playerRating">The player's current rating.</param>
	/// <param name="opponentRating">The opponent's current rating.</param>
	/// <returns>Expected score between 0 and 1.</returns>
	public static double ExpectedScore(int playerRating, int opponentRating)
	{
		double diff = opponentRating - playerRating;
		return 1.0 / (1.0 + Math.Pow(10, diff / 400.0));
	}

	/// <summary>
	///     Calculates the win probability for a player.
	/// </summary>
	/// <param name="playerRating">The player's rating.</param>
	/// <param name="opponentRating">The opponent's rating.</param>
	/// <returns>Win probability as a percentage (0-100).</returns>
	public static double WinProbability(int playerRating, int opponentRating) =>
		ExpectedScore(playerRating, opponentRating) * 100.0;

	/// <summary>
	///     Calculates the rating change for a player.
	/// </summary>
	/// <param name="playerRating">The player's current rating.</param>
	/// <param name="opponentRating">The opponent's current rating.</param>
	/// <param name="actualScore">The actual score (1 for win, 0.5 for draw, 0 for loss).</param>
	/// <param name="kFactor">The K-factor to use.</param>
	/// <returns>The rating change (can be positive or negative).</returns>
	public static int Calculate(
		int    playerRating,
		int    opponentRating,
		double actualScore,
		int    kFactor = DefaultKFactor)
	{
		double expected = ExpectedScore(playerRating, opponentRating);
		return (int)Math.Round(kFactor * (actualScore - expected));
	}

	/// <summary>
	///     Estimates the rating gain/loss from multiple games.
	/// </summary>
	/// <param name="playerRating">Current player rating.</param>
	/// <param name="opponentRating">Average opponent rating.</param>
	/// <param name="wins">Number of wins.</param>
	/// <param name="draws">Number of draws.</param>
	/// <param name="losses">Number of losses.</param>
	/// <param name="kFactor">K-factor to use.</param>
	/// <returns>Total rating change.</returns>
	public static int EstimateMultipleGames(
		int playerRating,
		int opponentRating,
		int wins,
		int draws,
		int losses,
		int kFactor = DefaultKFactor)
	{
		double totalScore = wins * 1.0 + draws * 0.5 + losses * 0.0;
		int    totalGames = wins + draws + losses;

		if (totalGames == 0)
			return 0;

		double expectedPerGame = ExpectedScore(playerRating, opponentRating);
		double expectedTotal   = expectedPerGame * totalGames;

		return (int)Math.Round(kFactor * (totalScore - expectedTotal));
	}

	/// <summary>
	///     Calculates the rating change for a draw.
	/// </summary>
	public static int ForDraw(int playerRating, int opponentRating, int kFactor = DefaultKFactor) =>
		Calculate(playerRating, opponentRating, 0.5, kFactor);

	/// <summary>
	///     Calculates the rating change for a loss.
	/// </summary>
	public static int ForLoss(int playerRating, int opponentRating, int kFactor = DefaultKFactor) =>
		Calculate(playerRating, opponentRating, 0.0, kFactor);

	/// <summary>
	///     Calculates the rating change based on game result.
	/// </summary>
	/// <param name="playerRating">The player's current rating.</param>
	/// <param name="opponentRating">The opponent's current rating.</param>
	/// <param name="result">The game result.</param>
	/// <param name="playerColor">The player's color in the game.</param>
	/// <param name="kFactor">The K-factor to use.</param>
	/// <returns>The rating change for the player.</returns>
	public static int ForResult(
		int         playerRating,
		int         opponentRating,
		GameResult  result,
		PlayerColor playerColor,
		int         kFactor = DefaultKFactor)
	{
		double actualScore;

		if (result.IsDraw)
			actualScore = 0.5;
		else if (result.Winner.HasValue)
			actualScore = result.Winner.Value == playerColor ? 1.0 : 0.0;
		else
			// Game not finished
			return 0;

		return Calculate(playerRating, opponentRating, actualScore, kFactor);
	}

	/// <summary>
	///     Calculates the rating change for a win.
	/// </summary>
	public static int ForWin(int playerRating, int opponentRating, int kFactor = DefaultKFactor) =>
		Calculate(playerRating, opponentRating, 1.0, kFactor);

	/// <summary>
	///     Gets the appropriate K-factor for a player based on their profile.
	/// </summary>
	/// <param name="profile">The player's profile.</param>
	/// <returns>The K-factor to use for this player.</returns>
	public static int GetKFactor(PlayerProfile profile)
	{
		if (profile.TotalGames < 30)
			return NewPlayerKFactor;

		if (profile.Elo >= 2400)
			return MasterKFactor;

		return EstablishedKFactor;
	}
}
