namespace Bezoro.Events.Types;

/// <summary>
///     Type-erased wrapper for a queued event, capturing the dispatch action.
/// </summary>
internal sealed class QueuedEvent
{
	private readonly Action _dispatchAction;

	public QueuedEvent(Action dispatchAction)
	{
		_dispatchAction = dispatchAction;
	}

	public void Dispatch() => _dispatchAction();
}
