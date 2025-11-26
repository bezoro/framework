using System;
using Bezoro.Chess.API.Abstractions;

namespace Bezoro.Chess.API.Types;

/// <summary>
///     Configuration options for creating a chess game.
/// </summary>
/// <param name="EnginePath">Path to the UCI chess engine executable (required for all game types - needed for legal moves, classification, and evaluation).</param>
/// <param name="PlayerColor">The color the local player will play as.</param>
/// <param name="OpponentType">The type of opponent (Engine, LocalHuman, RemoteHuman).</param>
/// <param name="EngineDifficulty">Engine difficulty settings (for engine opponent).</param>
/// <param name="LocalPlayer">The local player's profile.</param>
/// <param name="LocalOpponent">The opponent's profile (for local two-player games).</param>
/// <param name="RemoteService">Remote game service (for online multiplayer).</param>
/// <param name="TimeControl">The time control for the game.</param>
/// <param name="EngineThinkTimeMs">Maximum time in milliseconds for engine to think per move.</param>
/// <param name="EngineDepth">Maximum search depth for the engine (null for time-based).</param>
/// <param name="AutoPlayOpponentMove">Whether opponent moves are played automatically.</param>
/// <param name="StartingFen">Custom starting position FEN (null for standard).</param>
/// <param name="ClassificationDepth">Depth for move classification analysis.</param>
public readonly record struct ChessGameOptions(
	string?              EnginePath           = null,
	PlayerColor          PlayerColor          = PlayerColor.White,
	OpponentType         OpponentType         = OpponentType.Engine,
	EngineDifficulty?    EngineDifficulty     = null,
	PlayerProfile?       LocalPlayer          = null,
	PlayerProfile?       LocalOpponent        = null,
	IRemoteGameService?  RemoteService        = null,
	GameClock?           TimeControl          = null,
	int                  EngineThinkTimeMs    = 1000,
	uint?                EngineDepth          = null,
	bool                 AutoPlayOpponentMove = true,
	string?              StartingFen          = null,
	uint                 ClassificationDepth  = 6
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
	///     Gets the opponent's color (opposite of player color).
	/// </summary>
	public PlayerColor OpponentColor => PlayerColor.Opponent();

	/// <summary>
	///     Gets the effective engine difficulty (defaults to Medium if not specified).
	/// </summary>
	public EngineDifficulty EffectiveDifficulty => EngineDifficulty ?? Types.EngineDifficulty.Medium;

	/// <summary>
	///     Gets whether this is an engine game.
	/// </summary>
	public bool IsEngineGame => OpponentType == OpponentType.Engine;

	/// <summary>
	///     Gets whether this is a local two-player game.
	/// </summary>
	public bool IsLocalMultiplayer => OpponentType == OpponentType.LocalHuman;

	/// <summary>
	///     Gets whether this is an online multiplayer game.
	/// </summary>
	public bool IsOnlineMultiplayer => OpponentType == OpponentType.RemoteHuman;

	// ============ Factory Methods for Engine Games ============

	/// <summary>
	///     Creates options for playing against the engine.
	/// </summary>
	/// <param name="enginePath">Path to the UCI engine executable.</param>
	/// <param name="difficulty">Engine difficulty level.</param>
	/// <param name="playerColor">The color you want to play as.</param>
	/// <param name="localPlayer">Optional player profile.</param>
	public static ChessGameOptions VsEngine(
		string            enginePath,
		EngineDifficulty? difficulty  = null,
		PlayerColor       playerColor = PlayerColor.White,
		PlayerProfile?    localPlayer = null) => new(
		enginePath,
		playerColor,
		OpponentType.Engine,
		difficulty,
		localPlayer
	);

	/// <summary>
	///     Creates options for playing against the engine at beginner difficulty.
	/// </summary>
	public static ChessGameOptions VsEngineBeginner(
		string         enginePath,
		PlayerColor    playerColor = PlayerColor.White,
		PlayerProfile? localPlayer = null) => VsEngine(
		enginePath,
		Types.EngineDifficulty.Beginner,
		playerColor,
		localPlayer
	);

	/// <summary>
	///     Creates options for playing against the engine at medium difficulty.
	/// </summary>
	public static ChessGameOptions VsEngineMedium(
		string         enginePath,
		PlayerColor    playerColor = PlayerColor.White,
		PlayerProfile? localPlayer = null) => VsEngine(
		enginePath,
		Types.EngineDifficulty.Medium,
		playerColor,
		localPlayer
	);

	/// <summary>
	///     Creates options for playing against the engine at maximum difficulty.
	/// </summary>
	public static ChessGameOptions VsEngineMaximum(
		string         enginePath,
		PlayerColor    playerColor = PlayerColor.White,
		PlayerProfile? localPlayer = null) => VsEngine(
		enginePath,
		Types.EngineDifficulty.Maximum,
		playerColor,
		localPlayer
	);

	// ============ Factory Methods for Local Two-Player ============

	/// <summary>
	///     Creates options for a local two-player game (same device).
	///     The engine is required for legal move calculation, move classification, and evaluation.
	/// </summary>
	/// <param name="enginePath">Path to the UCI engine executable (required for legal moves and analysis).</param>
	/// <param name="player1">First player's profile (plays as white).</param>
	/// <param name="player2">Second player's profile (plays as black).</param>
	/// <param name="timeControl">Optional time control.</param>
	public static ChessGameOptions LocalTwoPlayer(
		string         enginePath,
		PlayerProfile  player1,
		PlayerProfile  player2,
		GameClock?     timeControl = null) => new(
		enginePath,
		PlayerColor: PlayerColor.White,
		OpponentType: OpponentType.LocalHuman,
		EngineDifficulty: null,
		LocalPlayer: player1,
		LocalOpponent: player2,
		RemoteService: null,
		TimeControl: timeControl,
		AutoPlayOpponentMove: false
	);

	/// <summary>
	///     Creates options for a local two-player game with generated profiles.
	///     The engine is required for legal move calculation, move classification, and evaluation.
	/// </summary>
	/// <param name="enginePath">Path to the UCI engine executable (required for legal moves and analysis).</param>
	/// <param name="player1Name">First player's name.</param>
	/// <param name="player2Name">Second player's name.</param>
	/// <param name="timeControl">Optional time control.</param>
	public static ChessGameOptions LocalTwoPlayer(
		string     enginePath,
		string     player1Name,
		string     player2Name,
		GameClock? timeControl = null) => LocalTwoPlayer(
		enginePath,
		PlayerProfile.Create(player1Name),
		PlayerProfile.Create(player2Name),
		timeControl
	);

	// ============ Factory Methods for Online Multiplayer ============

	/// <summary>
	///     Creates options for an online multiplayer game.
	///     The engine is required for legal move calculation, move classification, and evaluation.
	/// </summary>
	/// <param name="enginePath">Path to the UCI engine executable (required for legal moves and analysis).</param>
	/// <param name="remoteService">The remote game service implementation.</param>
	/// <param name="localPlayer">The local player's profile.</param>
	/// <param name="yourColor">Your assigned color.</param>
	/// <param name="opponentProfile">The opponent's profile.</param>
	/// <param name="timeControl">Optional time control.</param>
	public static ChessGameOptions OnlineMultiplayer(
		string             enginePath,
		IRemoteGameService remoteService,
		PlayerProfile      localPlayer,
		PlayerColor        yourColor,
		PlayerProfile      opponentProfile,
		GameClock?         timeControl = null) => new(
		enginePath,
		PlayerColor: yourColor,
		OpponentType: OpponentType.RemoteHuman,
		EngineDifficulty: null,
		LocalPlayer: localPlayer,
		LocalOpponent: opponentProfile,
		RemoteService: remoteService,
		TimeControl: timeControl,
		AutoPlayOpponentMove: false
	);

	// ============ Legacy Factory Methods (for backward compatibility) ============

	/// <summary>
	///     Gets the default options with just the engine path required.
	/// </summary>
	public static ChessGameOptions Default(string enginePath) => VsEngine(enginePath);

	/// <summary>
	///     Creates options for an analysis game (unlimited time, no auto-play).
	/// </summary>
	public static ChessGameOptions Analysis(string enginePath) => new(
		enginePath,
		PlayerColor.White,
		OpponentType.Engine,
		Types.EngineDifficulty.Maximum,
		AutoPlayOpponentMove: false
	);

	/// <summary>
	///     Creates options for a blitz game (5 minutes) against engine.
	/// </summary>
	public static ChessGameOptions Blitz(string enginePath, PlayerColor playerColor = PlayerColor.White) => new(
		enginePath,
		playerColor,
		OpponentType.Engine,
		TimeControl: GameClock.Blitz5Min
	);

	/// <summary>
	///     Creates options for a bullet game (1 minute) against engine.
	/// </summary>
	public static ChessGameOptions Bullet(string enginePath, PlayerColor playerColor = PlayerColor.White) => new(
		enginePath,
		playerColor,
		OpponentType.Engine,
		TimeControl: GameClock.Bullet1Min
	);

	/// <summary>
	///     Creates options for a rapid game (10 minutes) against engine.
	/// </summary>
	public static ChessGameOptions Rapid(string enginePath, PlayerColor playerColor = PlayerColor.White) => new(
		enginePath,
		playerColor,
		OpponentType.Engine,
		TimeControl: GameClock.Rapid10Min
	);

	/// <summary>
	///     Creates options for a game where the player plays as white.
	/// </summary>
	public static ChessGameOptions PlayAsWhite(string enginePath, GameClock? timeControl = null) => new(
		enginePath,
		PlayerColor.White,
		OpponentType.Engine,
		TimeControl: timeControl ?? GameClock.Unlimited
	);

	/// <summary>
	///     Creates options for a game where the player plays as black.
	/// </summary>
	public static ChessGameOptions PlayAsBlack(string enginePath, GameClock? timeControl = null) => new(
		enginePath,
		PlayerColor.Black,
		OpponentType.Engine,
		TimeControl: timeControl ?? GameClock.Unlimited
	);

	// ============ With Methods ============

	/// <summary>
	///     Creates a copy with a different opponent type.
	/// </summary>
	public ChessGameOptions WithOpponentType(OpponentType type) => this with { OpponentType = type };

	/// <summary>
	///     Creates a copy with a different engine difficulty.
	/// </summary>
	public ChessGameOptions WithDifficulty(EngineDifficulty difficulty) => this with { EngineDifficulty = difficulty };

	/// <summary>
	///     Creates a copy with a different player profile.
	/// </summary>
	public ChessGameOptions WithLocalPlayer(PlayerProfile player) => this with { LocalPlayer = player };

	/// <summary>
	///     Creates a copy with a different opponent profile.
	/// </summary>
	public ChessGameOptions WithLocalOpponent(PlayerProfile opponent) => this with { LocalOpponent = opponent };

	/// <summary>
	///     Creates a copy with auto-play enabled or disabled.
	/// </summary>
	public ChessGameOptions WithAutoPlay(bool autoPlay) => this with { AutoPlayOpponentMove = autoPlay };

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

	/// <summary>
	///     Creates a copy with a remote game service.
	/// </summary>
	public ChessGameOptions WithRemoteService(IRemoteGameService service) => this with { RemoteService = service };
}
