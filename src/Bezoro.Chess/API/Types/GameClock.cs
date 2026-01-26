using System;

namespace Bezoro.Chess.API.Types;

/// <summary>
///     Represents the type of time control.
/// </summary>
public enum TimeControlType
{
	/// <summary>No time limit.</summary>
	Unlimited,

	/// <summary>Bullet chess (typically 1-2 minutes).</summary>
	Bullet,

	/// <summary>Blitz chess (typically 3-5 minutes).</summary>
	Blitz,

	/// <summary>Rapid chess (typically 10-15 minutes).</summary>
	Rapid,

	/// <summary>Classical chess (typically 30+ minutes).</summary>
	Classical,

	/// <summary>Custom time control.</summary>
	Custom
}

/// <summary>
///     Represents the chess game clock state for both players.
///     Designed for Unity consumption to display time remaining.
/// </summary>
/// <param name="WhiteTimeMs">White's remaining time in milliseconds.</param>
/// <param name="BlackTimeMs">Black's remaining time in milliseconds.</param>
/// <param name="IncrementMs">Time increment per move in milliseconds.</param>
/// <param name="Type">The type of time control.</param>
/// <param name="IsRunning">Whether the clock is currently running.</param>
/// <param name="ActiveColor">Which player's clock is currently running.</param>
public readonly record struct GameClock(
	long            WhiteTimeMs,
	long            BlackTimeMs,
	long            IncrementMs,
	TimeControlType Type,
	bool            IsRunning,
	PlayerColor     ActiveColor
)
{
	/// <summary>
	///     Creates a time control with the specified initial time and increment.
	/// </summary>
	/// <param name="initialTime">Initial time for each player.</param>
	/// <param name="increment">Time increment per move.</param>
	public static GameClock Create(TimeSpan initialTime, TimeSpan increment)
	{
		var type = ClassifyTimeControl(initialTime, increment);
		return new(
			(long)initialTime.TotalMilliseconds,
			(long)initialTime.TotalMilliseconds,
			(long)increment.TotalMilliseconds,
			type,
			false,
			PlayerColor.White
		);
	}

	/// <summary>
	///     Creates a blitz time control (3 minutes per side).
	/// </summary>
	public static GameClock Blitz3Min { get; } = Create(TimeSpan.FromMinutes(3), TimeSpan.Zero);

	/// <summary>
	///     Creates a blitz time control (5 minutes per side).
	/// </summary>
	public static GameClock Blitz5Min { get; } = Create(TimeSpan.FromMinutes(5), TimeSpan.Zero);

	/// <summary>
	///     Creates a blitz time control (5 minutes per side, 3 second increment).
	/// </summary>
	public static GameClock Blitz5Plus3 { get; } = Create(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(3));

	/// <summary>
	///     Creates a bullet time control (1 minute per side).
	/// </summary>
	public static GameClock Bullet1Min { get; } = Create(TimeSpan.FromMinutes(1), TimeSpan.Zero);

	/// <summary>
	///     Creates a bullet time control (2 minutes per side, 1 second increment).
	/// </summary>
	public static GameClock Bullet2Plus1 { get; } = Create(TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(1));

	/// <summary>
	///     Creates a classical time control (30 minutes per side).
	/// </summary>
	public static GameClock Classical30Min { get; } = Create(TimeSpan.FromMinutes(30), TimeSpan.Zero);

	/// <summary>
	///     Creates a rapid time control (10 minutes per side).
	/// </summary>
	public static GameClock Rapid10Min { get; } = Create(TimeSpan.FromMinutes(10), TimeSpan.Zero);

	/// <summary>
	///     Creates a rapid time control (15 minutes per side, 10 second increment).
	/// </summary>
	public static GameClock Rapid15Plus10 { get; } = Create(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(10));

	/// <summary>
	///     Gets an unlimited time control clock (no time limit).
	/// </summary>
	public static GameClock Unlimited { get; } = new(
		long.MaxValue,
		long.MaxValue,
		0,
		TimeControlType.Unlimited,
		false,
		PlayerColor.White
	);

	/// <summary>
	///     Gets whether black's time has run out.
	/// </summary>
	public bool IsBlackTimeout => BlackTimeMs <= 0 && Type != TimeControlType.Unlimited;

	/// <summary>
	///     Gets whether any player has run out of time.
	/// </summary>
	public bool IsTimeout => IsWhiteTimeout || IsBlackTimeout;

	/// <summary>
	///     Gets whether this is an unlimited time control.
	/// </summary>
	public bool IsUnlimited => Type == TimeControlType.Unlimited;

	/// <summary>
	///     Gets whether white's time has run out.
	/// </summary>
	public bool IsWhiteTimeout => WhiteTimeMs <= 0 && Type != TimeControlType.Unlimited;

	/// <summary>
	///     Gets the player who ran out of time, or null if no timeout.
	/// </summary>
	public PlayerColor? TimedOutPlayer =>
		IsWhiteTimeout ? PlayerColor.White : IsBlackTimeout ? PlayerColor.Black : null;

	/// <summary>
	///     Gets a formatted display string for black's time (MM:SS or M:SS.s).
	/// </summary>
	public string BlackTimeDisplay => FormatTime(BlackTimeMs);

	/// <summary>
	///     Gets a formatted display string for white's time (MM:SS or M:SS.s).
	/// </summary>
	public string WhiteTimeDisplay => FormatTime(WhiteTimeMs);

	/// <summary>
	///     Gets black's remaining time as a TimeSpan.
	/// </summary>
	public TimeSpan BlackTime => TimeSpan.FromMilliseconds(BlackTimeMs);

	/// <summary>
	///     Gets the increment as a TimeSpan.
	/// </summary>
	public TimeSpan Increment => TimeSpan.FromMilliseconds(IncrementMs);

	/// <summary>
	///     Gets white's remaining time as a TimeSpan.
	/// </summary>
	public TimeSpan WhiteTime => TimeSpan.FromMilliseconds(WhiteTimeMs);

	/// <summary>
	///     Sets the active color's clock.
	/// </summary>
	public GameClock SetActiveColor(PlayerColor color) => this with { ActiveColor = color };

	/// <summary>
	///     Starts the clock.
	/// </summary>
	public GameClock Start() => this with { IsRunning = true };

	/// <summary>
	///     Stops the clock.
	/// </summary>
	public GameClock Stop() => this with { IsRunning = false };

	/// <summary>
	///     Switches the active clock and adds increment to the player who just moved.
	/// </summary>
	/// <param name="movedColor">The color that just completed their move.</param>
	public GameClock SwitchTurn(PlayerColor movedColor)
	{
		if (Type == TimeControlType.Unlimited)
			return this with { ActiveColor = movedColor.Opponent() };

		return movedColor == PlayerColor.White
				   ? this with
				   {
					   WhiteTimeMs = WhiteTimeMs + IncrementMs,
					   ActiveColor = PlayerColor.Black
				   }
				   : this with
				   {
					   BlackTimeMs = BlackTimeMs + IncrementMs,
					   ActiveColor = PlayerColor.White
				   };
	}

	/// <summary>
	///     Subtracts elapsed time from the active player's clock.
	/// </summary>
	/// <param name="elapsedMs">Elapsed time in milliseconds.</param>
	public GameClock Tick(long elapsedMs)
	{
		if (!IsRunning || Type == TimeControlType.Unlimited)
			return this;

		return ActiveColor == PlayerColor.White
				   ? this with { WhiteTimeMs = Math.Max(0, WhiteTimeMs - elapsedMs) }
				   : this with { BlackTimeMs = Math.Max(0, BlackTimeMs - elapsedMs) };
	}

	/// <summary>
	///     Gets the remaining time in milliseconds for the specified player.
	/// </summary>
	public long GetTimeRemainingMs(PlayerColor color) =>
		color == PlayerColor.White ? WhiteTimeMs : BlackTimeMs;

	/// <summary>
	///     Gets the remaining time for the specified player.
	/// </summary>
	public TimeSpan GetTimeRemaining(PlayerColor color) =>
		color == PlayerColor.White ? WhiteTime : BlackTime;

	private static string FormatTime(long ms)
	{
		if (ms == long.MaxValue) return "∞";

		if (ms < 0) ms = 0;

		var ts = TimeSpan.FromMilliseconds(ms);

		// Under 20 seconds: show tenths
		if (ms < 20_000)
			return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds / 100}";

		// Under 1 hour: show MM:SS
		if (ms < 3_600_000)
			return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";

		// Over 1 hour: show H:MM:SS
		return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
	}

	private static TimeControlType ClassifyTimeControl(TimeSpan initialTime, TimeSpan increment)
	{
		double totalMs = initialTime.TotalMilliseconds + increment.TotalMilliseconds * 40; // Estimate 40 moves

		return totalMs switch
		{
			<= 180_000   => TimeControlType.Bullet, // < 3 min expected
			<= 480_000   => TimeControlType.Blitz,  // 3-8 min expected
			<= 1_500_000 => TimeControlType.Rapid,  // 8-25 min expected
			_            => TimeControlType.Classical
		};
	}
}
