namespace Bezoro.Statecharts;

/// <summary>Identifies one entry into one state of one runner. Default is invalid.</summary>
/// <remarks>Opaque process-local identity. Do not serialize it or use it with another runner.</remarks>
public readonly struct StateActivation : IEquatable<StateActivation>
{
    internal StateActivation(long owner, ulong serial) { Owner = owner; Serial = serial; }
    internal long Owner { get; }
    internal ulong Serial { get; }
    /// <summary>Gets whether this token was issued by a runner, without establishing that it remains active.</summary>
    public bool IsValid => Owner != 0 && Serial != 0;
    /// <inheritdoc />
    public bool Equals(StateActivation other) => Owner == other.Owner && Serial == other.Serial;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is StateActivation other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => unchecked(Owner.GetHashCode() * 397 ^ Serial.GetHashCode());
    /// <summary>Compares activation identities.</summary>
    public static bool operator ==(StateActivation left, StateActivation right) => left.Equals(right);
    /// <summary>Compares activation identities.</summary>
    public static bool operator !=(StateActivation left, StateActivation right) => !left.Equals(right);
}
