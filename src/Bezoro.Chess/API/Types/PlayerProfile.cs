using System;

namespace Bezoro.Chess.API.Types;

/// <summary>
///     Represents a player's profile with statistics and identification.
///     Used for tracking player progress, ELO rating, and display in UI.
/// </summary>
/// <param name="Id">Unique identifier for the player.</param>
/// <param name="DisplayName">The player's display name.</param>
/// <param name="Elo">The player's ELO rating.</param>
/// <param name="Wins">Total number of wins.</param>
/// <param name="Losses">Total number of losses.</param>
/// <param name="Draws">Total number of draws.</param>
/// <param name="ProfileImage">Optional profile image as byte array (PNG/JPEG).</param>
/// <param name="CreatedAt">When the profile was created.</param>
/// <param name="LastPlayedAt">When the player last played a game.</param>
public readonly record struct PlayerProfile(
	string    Id,
	string    DisplayName,
	int       Elo,
	int       Wins,
	int       Losses,
	int       Draws,
	byte[]?   ProfileImage,
	DateTime  CreatedAt,
	DateTime  LastPlayedAt
)
{
	/// <summary>
	///     Default starting ELO for new players.
	/// </summary>
	public const int DefaultElo = 1200;

	/// <summary>
	///     Creates a new player profile with default values.
	/// </summary>
	/// <param name="displayName">The player's display name.</param>
	/// <param name="id">Optional unique ID (generates GUID if not provided).</param>
	public static PlayerProfile Create(string displayName, string? id = null) => new(
		id ?? Guid.NewGuid().ToString(),
		displayName,
		DefaultElo,
		0,
		0,
		0,
		null,
		DateTime.UtcNow,
		DateTime.UtcNow
	);

	/// <summary>
	///     Creates an engine profile with the specified name and ELO.
	/// </summary>
	/// <param name="engineName">Name of the engine (e.g., "Stockfish").</param>
	/// <param name="elo">The engine's ELO rating.</param>
	public static PlayerProfile CreateEngine(string engineName, int elo) => new(
		$"engine:{engineName.ToLowerInvariant()}",
		engineName,
		elo,
		0,
		0,
		0,
		null,
		DateTime.UtcNow,
		DateTime.UtcNow
	);

	/// <summary>
	///     Gets the total number of games played.
	/// </summary>
	public int TotalGames => Wins + Losses + Draws;

	/// <summary>
	///     Gets the win rate as a percentage (0-100).
	///     Returns 0 if no games have been played.
	/// </summary>
	public double WinRate => TotalGames == 0 ? 0.0 : (double)Wins / TotalGames * 100.0;

	/// <summary>
	///     Gets the win rate as a decimal (0.0-1.0).
	/// </summary>
	public double WinRateDecimal => TotalGames == 0 ? 0.0 : (double)Wins / TotalGames;

	/// <summary>
	///     Gets the loss rate as a percentage (0-100).
	/// </summary>
	public double LossRate => TotalGames == 0 ? 0.0 : (double)Losses / TotalGames * 100.0;

	/// <summary>
	///     Gets the draw rate as a percentage (0-100).
	/// </summary>
	public double DrawRate => TotalGames == 0 ? 0.0 : (double)Draws / TotalGames * 100.0;

	/// <summary>
	///     Gets a formatted rating string (e.g., "1200 ELO").
	/// </summary>
	public string Rating => $"{Elo} ELO";

	/// <summary>
	///     Gets the rating tier based on ELO.
	/// </summary>
	public RatingTier Tier => Elo switch
	{
		< 800   => RatingTier.Beginner,
		< 1000  => RatingTier.Novice,
		< 1200  => RatingTier.Intermediate,
		< 1400  => RatingTier.Amateur,
		< 1600  => RatingTier.Advanced,
		< 1800  => RatingTier.Expert,
		< 2000  => RatingTier.Master,
		< 2200  => RatingTier.NationalMaster,
		< 2400  => RatingTier.InternationalMaster,
		< 2700  => RatingTier.Grandmaster,
		_       => RatingTier.SuperGrandmaster
	};

	/// <summary>
	///     Gets whether this is an engine profile.
	/// </summary>
	public bool IsEngine => Id.StartsWith("engine:", StringComparison.Ordinal);

	/// <summary>
	///     Gets whether this profile has a profile image.
	/// </summary>
	public bool HasProfileImage => ProfileImage is { Length: > 0 };

	/// <summary>
	///     Creates a copy with updated stats after a game.
	/// </summary>
	/// <param name="result">The game result.</param>
	/// <param name="eloChange">The change in ELO (positive or negative).</param>
	public PlayerProfile WithGameResult(GameResult result, int eloChange)
	{
		var newWins = Wins;
		var newLosses = Losses;
		var newDraws = Draws;

		if (result.IsDraw)
			newDraws++;
		else if (result.Winner.HasValue)
			newWins++;  // This would need context about which color we are
		else
			newLosses++;

		return this with
		{
			Elo = Math.Max(100, Elo + eloChange), // Floor at 100 ELO
			Wins = newWins,
			Losses = newLosses,
			Draws = newDraws,
			LastPlayedAt = DateTime.UtcNow
		};
	}

	/// <summary>
	///     Creates a copy with a win recorded.
	/// </summary>
	/// <param name="eloChange">The ELO points gained.</param>
	public PlayerProfile WithWin(int eloChange) => this with
	{
		Elo = Elo + eloChange,
		Wins = Wins + 1,
		LastPlayedAt = DateTime.UtcNow
	};

	/// <summary>
	///     Creates a copy with a loss recorded.
	/// </summary>
	/// <param name="eloChange">The ELO points lost (should be negative).</param>
	public PlayerProfile WithLoss(int eloChange) => this with
	{
		Elo = Math.Max(100, Elo + eloChange),
		Losses = Losses + 1,
		LastPlayedAt = DateTime.UtcNow
	};

	/// <summary>
	///     Creates a copy with a draw recorded.
	/// </summary>
	/// <param name="eloChange">The ELO change (can be positive or negative).</param>
	public PlayerProfile WithDraw(int eloChange) => this with
	{
		Elo = Math.Max(100, Elo + eloChange),
		Draws = Draws + 1,
		LastPlayedAt = DateTime.UtcNow
	};

	/// <summary>
	///     Creates a copy with a new profile image.
	/// </summary>
	public PlayerProfile WithProfileImage(byte[] image) => this with { ProfileImage = image };

	/// <summary>
	///     Creates a copy with a new display name.
	/// </summary>
	public PlayerProfile WithDisplayName(string name) => this with { DisplayName = name };
}

/// <summary>
///     Rating tier based on ELO ranges.
/// </summary>
public enum RatingTier
{
	/// <summary>Below 800 ELO.</summary>
	Beginner,

	/// <summary>800-999 ELO.</summary>
	Novice,

	/// <summary>1000-1199 ELO.</summary>
	Intermediate,

	/// <summary>1200-1399 ELO.</summary>
	Amateur,

	/// <summary>1400-1599 ELO.</summary>
	Advanced,

	/// <summary>1600-1799 ELO.</summary>
	Expert,

	/// <summary>1800-1999 ELO.</summary>
	Master,

	/// <summary>2000-2199 ELO.</summary>
	NationalMaster,

	/// <summary>2200-2399 ELO.</summary>
	InternationalMaster,

	/// <summary>2400-2699 ELO.</summary>
	Grandmaster,

	/// <summary>2700+ ELO.</summary>
	SuperGrandmaster
}

