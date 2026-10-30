namespace Bezoro.BehaviorTrees;

/// <summary>Identifies one action activation in one runner. Tokens are not durable IDs.</summary>
public readonly struct BehaviorActivation : IEquatable<BehaviorActivation>
{
    internal BehaviorActivation(long owner, ulong serial)
    { Owner = owner; Serial = serial; }
    /// <summary>Gets the owning runner's process-local identity; zero identifies a default token.</summary>
    public long Owner { get; }
    /// <summary>Gets the activation serial, which is never reused by this runner.</summary>
    public ulong Serial { get; }
    /// <inheritdoc />
    public bool Equals(BehaviorActivation other) => Owner == other.Owner && Serial == other.Serial;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is BehaviorActivation other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => unchecked(Owner.GetHashCode() * 397 ^ Serial.GetHashCode());
    /// <summary>Compares activation identities.</summary>
    public static bool operator ==(BehaviorActivation left, BehaviorActivation right) => left.Equals(right);
    /// <summary>Compares activation identities.</summary>
    public static bool operator !=(BehaviorActivation left, BehaviorActivation right) => !left.Equals(right);
}
