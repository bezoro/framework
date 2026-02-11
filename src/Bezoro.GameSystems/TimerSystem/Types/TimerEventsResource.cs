using System.Collections.Generic;

namespace Bezoro.GameSystems.TimerSystem.Types;

/// <summary>
///     Resource queue used to consume timer lifecycle events from ECS systems.
/// </summary>
public sealed class TimerEventsResource
{
	private readonly Queue<TimerLifecycleEvent> _events = new();

	/// <summary>
	///     Gets current queued event count.
	/// </summary>
	public int Count => _events.Count;

	/// <summary>
	///     Enqueues a lifecycle event.
	/// </summary>
	/// <param name="eventData">Event payload to enqueue.</param>
	public void Enqueue(in TimerLifecycleEvent eventData) => _events.Enqueue(eventData);

	/// <summary>
	///     Attempts to dequeue the next lifecycle event.
	/// </summary>
	/// <param name="eventData">Dequeued event payload when available.</param>
	/// <returns><c>true</c> when an event was dequeued; otherwise <c>false</c>.</returns>
	public bool TryDequeue(out TimerLifecycleEvent eventData)
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
