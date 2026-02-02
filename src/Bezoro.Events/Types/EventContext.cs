namespace Bezoro.Events.Types;

/// <summary>
///     Context wrapper passed to event handlers, carrying the event data and a cancellation flag.
/// </summary>
/// <typeparam name="TEvent">The event struct type.</typeparam>
public sealed class EventContext<TEvent> where TEvent : struct
{
    /// <summary>
    ///     Creates a new event context wrapping the specified event data.
    /// </summary>
    public EventContext(TEvent data)
	{
		Data = data;
	}

    /// <summary>
    ///     The event data.
    /// </summary>
    public TEvent Data { get; }

    /// <summary>
    ///     Set to <c>true</c> to stop further handlers from processing this event.
    /// </summary>
    public bool Handled { get; set; }
}
