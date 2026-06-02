namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Describes an authored chess-clock snapshot to restore while loading a match position.
/// </summary>
/// <param name="WhiteRemaining">White's remaining time at <paramref name="SnapshotUtc" />.</param>
/// <param name="BlackRemaining">Black's remaining time at <paramref name="SnapshotUtc" />.</param>
/// <param name="ActiveColor">Side whose clock is active: <c>w</c> or <c>b</c>.</param>
/// <param name="DelayRemaining">Remaining delay before the active side's main clock decreases.</param>
/// <param name="IsPaused">Whether the clock should be restored in a paused state.</param>
/// <param name="WhiteMovesCompleted">Completed move count for White.</param>
/// <param name="BlackMovesCompleted">Completed move count for Black.</param>
/// <param name="ActiveStageIndex">Active time-control stage index to expose in the restored clock snapshot.</param>
/// <param name="SnapshotUtc">Timestamp represented by this clock snapshot.</param>
public readonly record struct PlayableMatchClockRestore(
	TimeSpan       WhiteRemaining,
	TimeSpan       BlackRemaining,
	char           ActiveColor,
	TimeSpan       DelayRemaining,
	bool           IsPaused,
	int            WhiteMovesCompleted,
	int            BlackMovesCompleted,
	int            ActiveStageIndex,
	DateTimeOffset SnapshotUtc
)
{
	/// <summary>
	///     Validates that the snapshot contains non-negative time values, move counts, and a valid active side.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when any snapshot value is outside its valid range.</exception>
	public void Validate()
	{
		if (WhiteRemaining < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(WhiteRemaining), "White remaining time cannot be negative.");

		if (BlackRemaining < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(BlackRemaining), "Black remaining time cannot be negative.");

		if (ActiveColor is not ('w' or 'b'))
			throw new ArgumentOutOfRangeException(nameof(ActiveColor), "Active color must be 'w' or 'b'.");

		if (DelayRemaining < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(DelayRemaining), "Delay remaining cannot be negative.");

		if (WhiteMovesCompleted < 0)
			throw new ArgumentOutOfRangeException(nameof(WhiteMovesCompleted), "White move count cannot be negative.");

		if (BlackMovesCompleted < 0)
			throw new ArgumentOutOfRangeException(nameof(BlackMovesCompleted), "Black move count cannot be negative.");

		if (ActiveStageIndex < 0)
			throw new ArgumentOutOfRangeException(nameof(ActiveStageIndex), "Active stage index cannot be negative.");
	}
}
