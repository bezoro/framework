using System;

namespace Bezoro.Chess.API.Types;

/// <summary>
///     Configuration options for creating a chess game.
/// </summary>
/// <param name="EnginePath">Path to the UCI chess engine executable.</param>
/// <param name="PlayerColor">The color the human player will play as.</param>
/// <param name="TimeControl">The time control for the game.</param>
/// <param name="EngineThinkTimeMs">Maximum time in milliseconds for engine to think per move.</param>
/// <param name="EngineDepth">Maximum search depth for the engine (null for time-based).</param>
/// <param name="AutoPlayEngineMove">Whether engine moves are played automatically.</param>
/// <param name="StartingFen">Custom starting position FEN (null for standard).</param>
/// <param name="ClassificationDepth">Depth for move classification analysis.</param>
public readonly record struct ChessGameOptions(
	string      EnginePath,
	PlayerColor PlayerColor         = PlayerColor.White,
	GameClock?  TimeControl         = null,
	int         EngineThinkTimeMs   = 1000,
	uint?       EngineDepth         = null,
	bool        AutoPlayEngineMove  = true,
	string?     StartingFen         = null,
	uint        ClassificationDepth = 6
)
{
    /// <summary>
    ///     Gets whether this is an unlimited time game.
    /// </summary>
    public bool IsUnlimitedTime => EffectiveTimeControl.IsUnlimited;

    /// <summary>
    ///     Gets the effective time control (defaults to Unlimited if not specified).
    /// </summary>
    public GameClock EffectiveTimeControl => TimeControl ?? GameClock.Unlimited;

    /// <summary>
    ///     Gets the engine color (opposite of player color).
    /// </summary>
    public PlayerColor EngineColor => PlayerColor.Opponent();

    /// <summary>
    ///     Creates options for an analysis game (unlimited time, no auto-play).
    /// </summary>
    public static ChessGameOptions Analysis(string enginePath) => new(
		enginePath,
		PlayerColor.White,
		GameClock.Unlimited,
		AutoPlayEngineMove: false
	);

    /// <summary>
    ///     Creates options for a blitz game (5 minutes).
    /// </summary>
    public static ChessGameOptions Blitz(string enginePath, PlayerColor playerColor = PlayerColor.White) => new(
		enginePath,
		playerColor,
		GameClock.Blitz5Min
	);

    /// <summary>
    ///     Creates options for a bullet game (1 minute).
    /// </summary>
    public static ChessGameOptions Bullet(string enginePath, PlayerColor playerColor = PlayerColor.White) => new(
		enginePath,
		playerColor,
		GameClock.Bullet1Min
	);

    /// <summary>
    ///     Gets the default options with just the engine path required.
    /// </summary>
    public static ChessGameOptions Default(string enginePath) => new(enginePath);

    /// <summary>
    ///     Creates options for a game where the player plays as black.
    /// </summary>
    public static ChessGameOptions PlayAsBlack(string enginePath, GameClock? timeControl = null) => new(
		enginePath,
		PlayerColor.Black,
		timeControl ?? GameClock.Unlimited
	);

    /// <summary>
    ///     Creates options for a game where the player plays as white.
    /// </summary>
    public static ChessGameOptions PlayAsWhite(string enginePath, GameClock? timeControl = null) => new(
		enginePath,
		PlayerColor.White,
		timeControl ?? GameClock.Unlimited
	);

    /// <summary>
    ///     Creates options for a rapid game (10 minutes).
    /// </summary>
    public static ChessGameOptions Rapid(string enginePath, PlayerColor playerColor = PlayerColor.White) => new(
		enginePath,
		playerColor,
		GameClock.Rapid10Min
	);

    /// <summary>
    ///     Creates a copy with auto-play enabled or disabled.
    /// </summary>
    public ChessGameOptions WithAutoPlay(bool autoPlay) => this with { AutoPlayEngineMove = autoPlay };

    /// <summary>
    ///     Creates a copy with a different engine depth.
    /// </summary>
    public ChessGameOptions WithEngineDepth(uint depth) => this with { EngineDepth = depth };

    /// <summary>
    ///     Creates a copy with a different engine think time.
    /// </summary>
    public ChessGameOptions WithEngineThinkTime(TimeSpan thinkTime) =>
		this with { EngineThinkTimeMs = (int)thinkTime.TotalMilliseconds };

    /// <summary>
    ///     Creates a copy with a different player color.
    /// </summary>
    public ChessGameOptions WithPlayerColor(PlayerColor color) => this with { PlayerColor = color };

    /// <summary>
    ///     Creates a copy with a custom starting position.
    /// </summary>
    public ChessGameOptions WithStartingFen(string fen) => this with { StartingFen = fen };

    /// <summary>
    ///     Creates a copy with a different time control.
    /// </summary>
    public ChessGameOptions WithTimeControl(GameClock clock) => this with { TimeControl = clock };
}
