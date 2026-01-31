namespace Bezoro.Events.Types;

/// <summary>
/// Internal record of a single event subscription.
/// </summary>
internal sealed class EventSubscription
{
    public SubscriptionHandle Handle { get; }
    public int Priority { get; }
    public Delegate Handler { get; }

    public EventSubscription(SubscriptionHandle handle, int priority, Delegate handler)
    {
        Handle = handle;
        Priority = priority;
        Handler = handler;
    }
}
