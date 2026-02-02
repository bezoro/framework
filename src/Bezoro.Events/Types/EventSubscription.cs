namespace Bezoro.Events.Types;

/// <summary>
///     Internal record of a single event subscription.
/// </summary>
internal sealed class EventSubscription
{
	public EventSubscription(SubscriptionHandle handle, int priority, Delegate handler)
	{
		Handle   = handle;
		Priority = priority;
		Handler  = handler;
	}

	public Delegate           Handler  { get; }
	public int                Priority { get; }
	public SubscriptionHandle Handle   { get; }
}
