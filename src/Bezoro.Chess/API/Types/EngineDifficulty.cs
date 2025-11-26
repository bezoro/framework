namespace Bezoro.Chess.API.Types;

/// <summary>
///     Represents engine difficulty settings with ELO-based configuration.
///     Uses UCI_LimitStrength and UCI_Elo options when supported,
///     with fallback to depth/time limits.
/// </summary>
/// <param name="Name">Display name for the difficulty level.</param>
/// <param name="Elo">Target ELO rating (800-2800+).</param>
/// <param name="MaxDepth">Fallback maximum search depth when UCI_Elo not supported.</param>
/// <param name="MaxThinkTimeMs">Fallback maximum think time in milliseconds.</param>
public readonly record struct EngineDifficulty(
	string Name,
	int    Elo,
	uint?  MaxDepth,
	int?   MaxThinkTimeMs
)
{
	/// <summary>
	///     Beginner difficulty (~800 ELO).
	///     Makes frequent mistakes, very short thinking time.
	/// </summary>
	public static EngineDifficulty Beginner { get; } = new(
		"Beginner",
		800,
		MaxDepth: 2,
		MaxThinkTimeMs: 100
	);

	/// <summary>
	///     Easy difficulty (~1000 ELO).
	///     Makes occasional blunders, limited depth.
	/// </summary>
	public static EngineDifficulty Easy { get; } = new(
		"Easy",
		1000,
		MaxDepth: 4,
		MaxThinkTimeMs: 200
	);

	/// <summary>
	///     Medium difficulty (~1200 ELO).
	///     Club player level, moderate thinking.
	/// </summary>
	public static EngineDifficulty Medium { get; } = new(
		"Medium",
		1200,
		MaxDepth: 6,
		MaxThinkTimeMs: 500
	);

	/// <summary>
	///     Intermediate difficulty (~1400 ELO).
	///     Strong club player level.
	/// </summary>
	public static EngineDifficulty Intermediate { get; } = new(
		"Intermediate",
		1400,
		MaxDepth: 8,
		MaxThinkTimeMs: 750
	);

	/// <summary>
	///     Hard difficulty (~1600 ELO).
	///     Expert level, few mistakes.
	/// </summary>
	public static EngineDifficulty Hard { get; } = new(
		"Hard",
		1600,
		MaxDepth: 10,
		MaxThinkTimeMs: 1000
	);

	/// <summary>
	///     Advanced difficulty (~1800 ELO).
	///     Very strong play.
	/// </summary>
	public static EngineDifficulty Advanced { get; } = new(
		"Advanced",
		1800,
		MaxDepth: 12,
		MaxThinkTimeMs: 1500
	);

	/// <summary>
	///     Expert difficulty (~2000 ELO).
	///     Master-level play.
	/// </summary>
	public static EngineDifficulty Expert { get; } = new(
		"Expert",
		2000,
		MaxDepth: 14,
		MaxThinkTimeMs: 2000
	);

	/// <summary>
	///     Master difficulty (~2200 ELO).
	///     National master level.
	/// </summary>
	public static EngineDifficulty Master { get; } = new(
		"Master",
		2200,
		MaxDepth: 16,
		MaxThinkTimeMs: 3000
	);

	/// <summary>
	///     Grandmaster difficulty (~2400 ELO).
	///     International master level.
	/// </summary>
	public static EngineDifficulty Grandmaster { get; } = new(
		"Grandmaster",
		2400,
		MaxDepth: 18,
		MaxThinkTimeMs: 4000
	);

	/// <summary>
	///     Maximum difficulty (full engine strength).
	///     No limitations, engine plays at full power.
	/// </summary>
	public static EngineDifficulty Maximum { get; } = new(
		"Maximum",
		3000,
		MaxDepth: null,
		MaxThinkTimeMs: null
	);

	/// <summary>
	///     Gets all predefined difficulty levels in order of increasing difficulty.
	/// </summary>
	public static EngineDifficulty[] AllLevels { get; } =
	[
		Beginner,
		Easy,
		Medium,
		Intermediate,
		Hard,
		Advanced,
		Expert,
		Master,
		Grandmaster,
		Maximum
	];

	/// <summary>
	///     Creates a custom difficulty with the specified ELO.
	///     Automatically calculates appropriate depth/time limits.
	/// </summary>
	/// <param name="elo">Target ELO rating.</param>
	/// <param name="name">Optional custom name.</param>
	public static EngineDifficulty Custom(int elo, string? name = null)
	{
		// Calculate fallback limits based on ELO
		// Higher ELO = deeper search and more time
		var depth = elo switch
		{
			< 900   => 2u,
			< 1100  => 4u,
			< 1300  => 6u,
			< 1500  => 8u,
			< 1700  => 10u,
			< 1900  => 12u,
			< 2100  => 14u,
			< 2300  => 16u,
			< 2500  => 18u,
			_       => (uint?)null
		};

		var thinkTime = elo switch
		{
			< 900   => 100,
			< 1100  => 200,
			< 1300  => 500,
			< 1500  => 750,
			< 1700  => 1000,
			< 1900  => 1500,
			< 2100  => 2000,
			< 2300  => 3000,
			< 2500  => 4000,
			_       => (int?)null
		};

		return new(
			name ?? $"Custom ({elo} ELO)",
			elo,
			depth,
			thinkTime
		);
	}

	/// <summary>
	///     Gets the closest predefined difficulty level for the specified ELO.
	/// </summary>
	public static EngineDifficulty FromElo(int elo)
	{
		foreach (var level in AllLevels)
		{
			if (elo <= level.Elo + 100)
				return level;
		}
		return Maximum;
	}

	/// <summary>
	///     Gets whether this difficulty uses full engine strength (no limits).
	/// </summary>
	public bool IsFullStrength => MaxDepth is null && MaxThinkTimeMs is null;

	/// <summary>
	///     Gets the rating tier corresponding to this difficulty's ELO.
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
}

