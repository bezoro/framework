using Xunit;

namespace Bezoro.Inventory.Tests;

public sealed class ItemCatalogTests
{
    [Fact]
    public void ItemId_ReservesZeroAndComparesByValue()
    {
        Assert.False(default(ItemId).IsValid);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemId(0));
        var item = new ItemId(uint.MaxValue);
        Assert.True(item.IsValid);
        Assert.Equal(uint.MaxValue, item.Value);
        Assert.Equal(item, new ItemId(uint.MaxValue));
        Assert.True(item == new ItemId(uint.MaxValue));
        Assert.True(item != new ItemId(1));
        Assert.True(item.Equals((object)new ItemId(uint.MaxValue)));
        Assert.False(item.Equals("item"));
        Assert.Equal(item.GetHashCode(), new ItemId(uint.MaxValue).GetHashCode());
    }

    [Fact]
    public void Definition_RequiresAValidItemAndPositiveLimit()
    {
        Assert.Throws<ArgumentException>(() => new ItemDefinition(default, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemDefinition(new ItemId(1), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemDefinition(new ItemId(1), -1));
        var definition = new ItemDefinition(new ItemId(1), int.MaxValue);
        Assert.Equal(int.MaxValue, definition.MaximumStackSize);
        Assert.Equal(new ItemId(1), definition.Item);
    }

    [Fact]
    public void Stack_DefaultIsEmptyAndConstructedStacksRequirePositiveQuantities()
    {
        Assert.Equal(8, System.Runtime.CompilerServices.Unsafe.SizeOf<ItemStack>());
        Assert.True(default(ItemStack).IsEmpty);
        Assert.Equal(default, default(ItemStack).Item);
        Assert.Equal(0, default(ItemStack).Quantity);
        Assert.Throws<ArgumentException>(() => new ItemStack(default, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemStack(new ItemId(1), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemStack(new ItemId(1), -1));
        var stack = new ItemStack(new ItemId(1), int.MaxValue);
        Assert.False(stack.IsEmpty);
        Assert.Equal(new ItemStack(new ItemId(1), int.MaxValue), stack);
    }

    [Fact]
    public void Catalog_CopiesDefinitionsAndPreservesInputOrder()
    {
        var source = new[] { new ItemDefinition(new ItemId(7), 20), new ItemDefinition(new ItemId(2), 5) };
        var catalog = new ItemCatalog(source);
        source[0] = new ItemDefinition(new ItemId(11), 99);
        Assert.Equal(2, catalog.Count);
        Assert.Equal(new ItemId(7), catalog.Definitions[0].Item);
        Assert.Equal(20, catalog.GetDefinition(new ItemId(7)).MaximumStackSize);
        Assert.True(catalog.TryGetDefinition(new ItemId(2), out var second));
        Assert.Equal(5, second.MaximumStackSize);
        Assert.False(catalog.TryGetDefinition(new ItemId(11), out var missing));
        Assert.Equal(default, missing);
    }

    [Fact]
    public void Catalog_RejectsDefaultDuplicateAndUnknownIdentifiers()
    {
        Assert.Throws<ArgumentException>(() => new ItemCatalog(new ItemDefinition[] { default }));
        Assert.Throws<ArgumentException>(() => new ItemCatalog(new[]
        {
            new ItemDefinition(new ItemId(1), 2), new ItemDefinition(new ItemId(1), 3)
        }));
        var catalog = new ItemCatalog(Array.Empty<ItemDefinition>());
        Assert.Empty(catalog.Definitions.ToArray());
        Assert.False(catalog.TryGetDefinition(default, out _));
        Assert.Throws<ArgumentException>(() => catalog.GetDefinition(default));
        Assert.Throws<ArgumentException>(() => catalog.GetDefinition(new ItemId(1)));
    }

    [Fact]
    public async Task Catalog_IsReadableByIndependentWorkers()
    {
        var definitions = Enumerable.Range(1, 128)
            .Select(value => new ItemDefinition(new ItemId((uint)value), value)).ToArray();
        var catalog = new ItemCatalog(definitions);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (int repeat = 0; repeat < 50; repeat++)
                foreach (var definition in definitions)
                    Assert.Equal(definition, catalog.GetDefinition(definition.Item));
        })));
    }
}
