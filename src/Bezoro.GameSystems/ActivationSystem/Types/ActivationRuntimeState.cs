namespace Bezoro.GameSystems.ActivationSystem.Types;

/// <summary>
///     Mutable runtime counters produced by activation ECS systems.
/// </summary>
public sealed class ActivationRuntimeState
{
	/// <summary>
	///     Gets the number of entries currently in <see cref="ActivationState.Activated" />.
	/// </summary>
	public int ActivatedCount { get; private set; }

	/// <summary>
	///     Gets whether there are no pending entries.
	/// </summary>
	public bool IsComplete => PendingCount == 0;

	/// <summary>
	///     Gets the number of entries currently in <see cref="ActivationState.Pending" />.
	/// </summary>
	public int PendingCount { get; private set; }

	internal bool CompletionPublished { get; set; }

	internal void SetCounts(int activatedCount, int pendingCount)
	{
		ActivatedCount = activatedCount;
		PendingCount = pendingCount;
	}
}
