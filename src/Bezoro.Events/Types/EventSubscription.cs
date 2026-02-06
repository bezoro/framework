namespace Bezoro.Events.Types;

/// <summary>
///     Internal record of a single event subscription.
/// </summary>
internal sealed class EventSubscription(SubscriptionHandle handle, int priority, Delegate handler)
{
	public Delegate           Handler  { get; } = handler;
	public int                Priority { get; } = priority;
	public SubscriptionHandle Handle   { get; } = handle;
}
