namespace Bezoro.Inventory;

/// <summary>A quantity of one item. The default value is the canonical empty stack.</summary>
/// <remarks>A stack has no catalog ownership. Insertion validates its identifier and quantity against the destination catalog.</remarks>
public readonly record struct ItemStack
{
    /// <summary>Creates a nonempty stack. Use the default value for an empty stack.</summary>
    public ItemStack(ItemId item, int quantity)
    {
        if (!item.IsValid) throw new ArgumentException("A valid item identifier is required.", nameof(item));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Item = item;
        Quantity = quantity;
    }

    /// <summary>Gets the item identifier, or the default identifier for an empty stack.</summary>
    public ItemId Item { get; }
    /// <summary>Gets the positive quantity, or zero for an empty stack.</summary>
    public int Quantity { get; }
    /// <summary>Gets whether the stack is empty.</summary>
    public bool IsEmpty => Quantity == 0;
}
