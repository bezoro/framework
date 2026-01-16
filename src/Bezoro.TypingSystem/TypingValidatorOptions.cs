using System;

namespace Bezoro.TypingSystem;

public sealed class TypingValidatorOptions
{
	public Action<TypingResult>? OnCompleted { get; init; }

	public Action<TypingResult>? OnFault { get; init; }

	public Action<TypingResult>? OnMatch { get; init; }

	public Action<TypingResult>? OnMismatch { get; init; }

	public Action<TypingResult>? OnValidated { get; init; }
	public bool                  IgnoreCase  { get; init; }

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
