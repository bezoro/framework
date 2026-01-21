using System;

namespace Bezoro.TypingSystem.Types;

/// <summary>
///     Provides options for configuring the typing validation process.
/// </summary>
public sealed class TypingValidatorOptions
{
	/// <summary>
	///     Action to execute when a word is successfully completed.
	/// </summary>
	public Action<TypingResult>? OnCompleted { get; init; }

	/// <summary>
	///     Action to execute when a validation fault occurs (e.g., out of range).
	/// </summary>
	public Action<TypingResult>? OnFault { get; init; }

	/// <summary>
	///     Action to execute when an input character matches.
	/// </summary>
	public Action<TypingResult>? OnMatch { get; init; }

	/// <summary>
	///     Action to execute when an input character does not match.
	/// </summary>
	public Action<TypingResult>? OnMismatch { get; init; }

	/// <summary>
	///     Action to execute after any validation has occurred.
	/// </summary>
	public Action<TypingResult>? OnValidated { get; init; }

	/// <summary>
	///     Gets or sets a value indicating whether to ignore case during validation.
	/// </summary>
	public bool                  IgnoreCase  { get; init; }

	/// <summary>
	///     Gets or sets the metrics collector to use during validation.
	/// </summary>
	public TypingMetrics? Metrics { get; init; }

	internal void Notify(TypingResult result)
	{
		Metrics?.Record(result);
		OnValidated?.Invoke(result);

		switch (result.Status)
		{
			case TypingValidationStatus.Match:
				OnMatch?.Invoke(result);
				break;
			case TypingValidationStatus.Completed:
				OnCompleted?.Invoke(result);
				break;
			case TypingValidationStatus.Mismatch:
				OnMismatch?.Invoke(result);
				break;
			case TypingValidationStatus.EmptyTarget:
			case TypingValidationStatus.PositionOutOfRange:
				OnFault?.Invoke(result);
				break;
		}
	}
}
