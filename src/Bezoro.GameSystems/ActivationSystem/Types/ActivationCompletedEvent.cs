namespace Bezoro.GameSystems.ActivationSystem.Types;

/// <summary>
///     Event payload emitted when all pending activation entries complete.
/// </summary>
public readonly struct ActivationCompletedEvent
{
	/// <summary>
	///     Initializes a completion event payload.
	/// </summary>
	/// <param name="activatedCount">Total activated entries at completion.</param>
	public ActivationCompletedEvent(int activatedCount)
	{
		ActivatedCount = activatedCount;
	}

	/// <summary>
	///     Gets the total activated entry count when completion was observed.
	/// </summary>
	public int ActivatedCount { get; }
}
