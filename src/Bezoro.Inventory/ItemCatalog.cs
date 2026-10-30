namespace Bezoro.Inventory;

/// <summary>Owns a copied, immutable set of item definitions.</summary>
/// <remarks>Share one catalog across inventories that exchange items. The catalog can be read concurrently.</remarks>
public sealed class ItemCatalog
{
    private readonly ItemDefinition[] _definitions;
    private readonly Dictionary<ItemId, int> _lookup;

    /// <summary>Copies definitions in input order. Empty catalogs are allowed; duplicate or default definitions are rejected.</summary>
    public ItemCatalog(ReadOnlySpan<ItemDefinition> definitions)
    {
        _definitions = definitions.ToArray();
        _lookup = new Dictionary<ItemId, int>(definitions.Length);
        for (int index = 0; index < _definitions.Length; index++)
        {
            var definition = _definitions[index];
            if (!definition.Item.IsValid || definition.MaximumStackSize <= 0)
                throw new ArgumentException("Every definition must have a valid identifier and positive stack limit.", nameof(definitions));
            if (_lookup.ContainsKey(definition.Item))
                throw new ArgumentException("Item identifiers must be unique within a catalog.", nameof(definitions));
            _lookup.Add(definition.Item, index);
        }
    }

    /// <summary>Gets the number of definitions.</summary>
    public int Count => _definitions.Length;
    /// <summary>Gets a read-only view of the copied definitions in input order.</summary>
    public ReadOnlySpan<ItemDefinition> Definitions => _definitions;

    /// <summary>Looks up a definition, returning false and the default definition for an unknown or default identifier.</summary>
    public bool TryGetDefinition(ItemId item, out ItemDefinition definition)
    {
        if (_lookup.TryGetValue(item, out int index))
        {
            definition = _definitions[index];
            return true;
        }
        definition = default;
        return false;
    }

    /// <summary>Gets a definition or throws <see cref="ArgumentException" /> for an unknown or default identifier.</summary>
    public ItemDefinition GetDefinition(ItemId item)
    {
        if (!_lookup.TryGetValue(item, out int index))
            throw new ArgumentException("The item identifier does not belong to this catalog.", nameof(item));
        return _definitions[index];
    }
}
