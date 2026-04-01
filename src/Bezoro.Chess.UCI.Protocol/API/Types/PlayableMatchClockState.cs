namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a snapshot of both clocks for the current match turn.
/// </summary>
/// <param name="WhiteRemaining">White's remaining time.</param>
/// <param name="BlackRemaining">Black's remaining time.</param>
/// <param name="ActiveColor">Side whose clock is currently running: <c>w</c> or <c>b</c>.</param>
/// <param name="DelayRemaining">Remaining delay before the active side's main clock decreases.</param>
/// <param name="IsPaused">Whether the match clock is currently paused.</param>
/// <param name="ActiveStageIndex">Currently active time-control stage index.</param>
/// <param name="SnapshotUtc">Timestamp used to produce this clock snapshot.</param>
public readonly record struct PlayableMatchClockState(
	TimeSpan       WhiteRemaining,
	TimeSpan       BlackRemaining,
	char           ActiveColor,
	TimeSpan       DelayRemaining,
	bool           IsPaused,
	int            ActiveStageIndex,
	DateTimeOffset SnapshotUtc
);
