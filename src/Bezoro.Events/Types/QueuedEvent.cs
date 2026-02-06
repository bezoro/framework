namespace Bezoro.Events.Types;

/// <summary>
///     Type-erased wrapper for a queued event, capturing the dispatch action.
/// </summary>
internal sealed class QueuedEvent(Action dispatchAction)
{
	public void Dispatch() => dispatchAction();
}
