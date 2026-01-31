namespace Bezoro.Events.Types;

/// <summary>
/// Lightweight handle identifying an event subscription.
/// </summary>
public readonly struct SubscriptionHandle : IEquatable<SubscriptionHandle>
{
    /// <summary>
    /// Represents an invalid/uninitialized handle.
    /// </summary>
    public static readonly SubscriptionHandle None = default;

    /// <summary>
    /// The unique identifier for this subscription.
    /// </summary>
    public readonly int Id;

    /// <summary>
    /// Creates a new subscription handle with the specified id.
    /// </summary>
    public SubscriptionHandle(int id) => Id = id;

    /// <summary>
    /// Whether this handle represents a valid subscription.
    /// </summary>
    public bool IsValid => Id > 0;

    /// <inheritdoc />
    public static bool operator ==(SubscriptionHandle left, SubscriptionHandle right) => left.Id == right.Id;

    /// <inheritdoc />
    public static bool operator !=(SubscriptionHandle left, SubscriptionHandle right) => left.Id != right.Id;

    /// <inheritdoc />
    public bool Equals(SubscriptionHandle other) => Id == other.Id;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SubscriptionHandle other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Id;

    /// <inheritdoc />
    public override string ToString() => $"Subscription({Id})";
}
