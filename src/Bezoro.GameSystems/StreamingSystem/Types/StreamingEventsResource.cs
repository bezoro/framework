using System.Collections.Generic;

namespace Bezoro.GameSystems.StreamingSystem.Types;

/// <summary>
///     Resource queue used to consume streaming transitions.
/// </summary>
public sealed class StreamingEventsResource
{
	private readonly Queue<StreamingStateChangedEvent> _events = new();

	/// <summary>
	///     Gets current queued event count.
	/// </summary>
	public int Count => _events.Count;

	/// <summary>
	///     Enqueues a streaming event.
	/// </summary>
	/// <param name="eventData">Event payload to enqueue.</param>
	public void Enqueue(in StreamingStateChangedEvent eventData) => _events.Enqueue(eventData);

	/// <summary>
	///     Attempts to dequeue the next streaming event.
	/// </summary>
	/// <param name="eventData">Dequeued event payload when available.</param>
	/// <returns><c>true</c> when an event was dequeued; otherwise <c>false</c>.</returns>
	public bool TryDequeue(out StreamingStateChangedEvent eventData)
	{
		if (_events.Count == 0)
		{
			eventData = default;
			return false;
		}

		eventData = _events.Dequeue();
		return true;
	}

	/// <summary>
	///     Clears all queued events.
	/// </summary>
	public void Clear() => _events.Clear();
}
