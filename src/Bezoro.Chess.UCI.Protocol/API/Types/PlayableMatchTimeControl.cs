using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Defines a symmetric chess clock configuration for both sides.
/// </summary>
public readonly record struct PlayableMatchTimeControl
{
	/// <summary>
	///     Creates a single-stage clock with optional increment and delay.
	/// </summary>
	public PlayableMatchTimeControl(
		TimeSpan                                initialTime,
		TimeSpan                                incrementPerMove,
		TimeSpan                                delayPerMove       = default,
		PlayableMatchTimeoutPolicy              timeoutPolicy      = PlayableMatchTimeoutPolicy.AutomaticLoss,
		ImmutableArray<PlayableMatchTimeControlStage> additionalStages = default)
	{
		InitialTime      = initialTime;
		IncrementPerMove = incrementPerMove;
		DelayPerMove     = delayPerMove;
		TimeoutPolicy    = timeoutPolicy;
		AdditionalStages = additionalStages.IsDefault ? [] : additionalStages;
	}

	/// <summary>
	///     Gets the initial time assigned to both sides.
	/// </summary>
	public TimeSpan InitialTime { get; }

	/// <summary>
	///     Gets the increment added to the moving side after each completed move in the base stage.
	/// </summary>
	public TimeSpan IncrementPerMove { get; }

	/// <summary>
	///     Gets the delay granted at the start of each turn before the running clock decreases.
	/// </summary>
	public TimeSpan DelayPerMove { get; }

	/// <summary>
	///     Gets any additional staged time controls.
	/// </summary>
	public ImmutableArray<PlayableMatchTimeControlStage> AdditionalStages { get; }

	/// <summary>
	///     Gets the timeout adjudication policy.
	/// </summary>
	public PlayableMatchTimeoutPolicy TimeoutPolicy { get; }

	/// <summary>
	///     Validates the configured time control values.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown when <see cref="InitialTime" /> is not positive or any configured increment/delay is negative.
	/// </exception>
	public void Validate()
	{
		if (InitialTime <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(InitialTime), "Initial time must be greater than zero.");

		if (IncrementPerMove < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(
				nameof(IncrementPerMove),
				"Increment per move must be greater than or equal to zero."
			);

		if (DelayPerMove < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(DelayPerMove), "Delay per move must be greater than or equal to zero.");

		foreach (var stage in AdditionalStages)
			stage.Validate();
	}
}
