using System.Collections.Generic;

namespace Bezoro.GameSystems.ActivationSystem.Types;

/// <summary>
///     Resource queue used to consume activation completion events.
/// </summary>
public sealed class ActivationEventsResource
{
	private readonly Queue<ActivationCompletedEvent> _events = new();

	/// <summary>
	///     Gets the current queued event count.
	/// </summary>
	public int Count => _events.Count;

	/// <summary>
	///     Clears all queued events.
	/// </summary>
	public void Clear() => _events.Clear();

	/// <summary>
	///     Enqueues a completion event.
	/// </summary>
	/// <param name="eventData">Event payload to enqueue.</param>
	public void Enqueue(in ActivationCompletedEvent eventData) => _events.Enqueue(eventData);

	/// <summary>
	///     Attempts to dequeue the next completion event.
	/// </summary>
	/// <param name="eventData">Dequeued event payload when available.</param>
	/// <returns><c>true</c> when an event was dequeued; otherwise <c>false</c>.</returns>
	public bool TryDequeue(out ActivationCompletedEvent eventData)
	{
		if (_events.Count == 0)
		{
			eventData = default;
			return false;
		}

		eventData = _events.Dequeue();
		return true;
	}
}
