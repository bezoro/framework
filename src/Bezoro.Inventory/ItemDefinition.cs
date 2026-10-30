namespace Bezoro.Inventory;

/// <summary>Defines the maximum quantity of one item in a slot.</summary>
public readonly record struct ItemDefinition
{
    /// <summary>Creates a definition for a valid identifier and a positive per-slot limit.</summary>
    public ItemDefinition(ItemId item, int maximumStackSize)
    {
        if (!item.IsValid) throw new ArgumentException("A valid item identifier is required.", nameof(item));
        if (maximumStackSize <= 0) throw new ArgumentOutOfRangeException(nameof(maximumStackSize));
        Item = item;
        MaximumStackSize = maximumStackSize;
    }

    /// <summary>Gets the item identifier.</summary>
    public ItemId Item { get; }
    /// <summary>Gets the positive per-slot quantity limit.</summary>
    public int MaximumStackSize { get; }
}
