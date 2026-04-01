namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents an additional stage in a staged chess clock.
/// </summary>
/// <param name="TriggerMovesPerSide">Number of completed moves by a side before this stage is granted.</param>
/// <param name="AddedTime">Additional time granted when the stage is reached.</param>
/// <param name="IncrementPerMove">Increment applied for subsequent moves after the stage is reached.</param>
/// <param name="DelayPerMove">Delay applied at the start of each turn after the stage is reached.</param>
public readonly record struct PlayableMatchTimeControlStage(
	int      TriggerMovesPerSide,
	TimeSpan AddedTime,
	TimeSpan IncrementPerMove,
	TimeSpan DelayPerMove
)
{
	/// <summary>
	///     Validates the configured stage values.
	/// </summary>
	public void Validate()
	{
		if (TriggerMovesPerSide <= 0)
			throw new ArgumentOutOfRangeException(
				nameof(TriggerMovesPerSide),
				"Trigger moves per side must be greater than zero."
			);

		if (AddedTime < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(AddedTime), "Added time must be greater than or equal to zero.");

		if (IncrementPerMove < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(
				nameof(IncrementPerMove),
				"Increment per move must be greater than or equal to zero."
			);

		if (DelayPerMove < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(DelayPerMove), "Delay per move must be greater than or equal to zero.");
	}
}
