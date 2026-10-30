namespace Bezoro.Inventory;

/// <summary>Identifies an application-defined item. Zero is reserved for an empty stack.</summary>
public readonly struct ItemId : IEquatable<ItemId>
{
    /// <summary>Creates an item identifier from a positive application-defined value.</summary>
    public ItemId(uint value)
    {
        if (value == 0) throw new ArgumentOutOfRangeException(nameof(value), "Item identifiers must be positive.");
        Value = value;
    }

    /// <summary>Gets the application-defined value; the default identifier has value zero.</summary>
    public uint Value { get; }
    /// <summary>Gets whether this identifier can identify an item.</summary>
    public bool IsValid => Value != 0;
    /// <inheritdoc />
    public bool Equals(ItemId other) => Value == other.Value;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ItemId other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => unchecked((int)Value);
    /// <summary>Compares two item identifiers.</summary>
    public static bool operator ==(ItemId left, ItemId right) => left.Equals(right);
    /// <summary>Compares two item identifiers.</summary>
    public static bool operator !=(ItemId left, ItemId right) => !left.Equals(right);
}
