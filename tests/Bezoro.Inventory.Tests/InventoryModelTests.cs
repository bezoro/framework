using Xunit;

namespace Bezoro.Inventory.Tests;

public sealed class InventoryModelTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 42)]
    [InlineData(7, 757)]
    [InlineData(32, 123456)]
    public void SeededMutations_MatchAnIndependentPlanAndCommitModel(int capacity, int seed)
    {
        int[] limits = { 0, 1, 4, 10, int.MaxValue };
        var catalog = new ItemCatalog(Enumerable.Range(1, 4)
            .Select(item => new ItemDefinition(new ItemId((uint)item), limits[item])).ToArray());
        var actual = new[] { new SlotInventory(catalog, capacity), new SlotInventory(catalog, capacity) };
        var expected = new[] { new Model(capacity, limits), new Model(capacity, limits) };
        var random = new Random(seed);
        for (int iteration = 0; iteration < 10_000; iteration++)
        {
            int owner = random.Next(2);
            uint item = (uint)random.Next(1, 5);
            long quantity = random.Next(1, 100);
            if (iteration % 97 == 0) quantity = long.MaxValue;
            var id = new ItemId(item);
            switch (random.Next(8))
            {
                case 0:
                    Assert.Equal(expected[owner].TryAdd(item, quantity), actual[owner].TryAdd(id, quantity));
                    break;
                case 1:
                    Assert.Equal(expected[owner].TryRemove(item, quantity), actual[owner].TryRemove(id, quantity));
                    break;
                case 2:
                {
                    int target = random.Next(2);
                    Assert.Equal(expected[owner].TryTransferTo(expected[target], item, quantity),
                        actual[owner].TryTransferTo(actual[target], id, quantity));
                    break;
                }
                case 3 when capacity > 0:
                {
                    int index = random.Next(capacity);
                    int amount = random.Next(1, 20);
                    Assert.Equal(expected[owner].TryInsertAt(index, item, amount),
                        actual[owner].TryInsertAt(index, new ItemStack(id, amount)));
                    break;
                }
                case 4 when capacity > 0:
                {
                    int index = random.Next(capacity);
                    int amount = random.Next(1, 20);
                    bool accepted = expected[owner].TryTakeAt(index, amount, out var taken);
                    Assert.Equal(accepted, actual[owner].TryTakeAt(index, amount, out var actualTaken));
                    Assert.Equal(taken.Item, actualTaken.Item.Value);
                    Assert.Equal(taken.Quantity, actualTaken.Quantity);
                    break;
                }
                case 5:
                    Assert.Equal(expected[owner].Quantity(item), actual[owner].GetQuantity(id));
                    Assert.Equal(expected[owner].Available(item), actual[owner].GetAvailableCapacity(id));
                    break;
                case 6 when iteration % 11 == 0:
                    expected[owner].Clear();
                    actual[owner].Clear();
                    break;
                default:
                {
                    var model = new Entry[capacity];
                    var snapshot = new ItemStack[capacity];
                    for (int index = 0; index < capacity; index++)
                    {
                        uint candidate = (uint)random.Next(5);
                        if (candidate == 0) continue;
                        int amount = random.Next(1, 12);
                        model[index] = new Entry(candidate, amount);
                        snapshot[index] = new ItemStack(new ItemId(candidate), amount);
                    }
                    Assert.Equal(expected[owner].TryRestore(model), actual[owner].TryRestore(snapshot));
                    break;
                }
            }
            for (int inventory = 0; inventory < 2; inventory++)
            {
                Assert.Equal(expected[inventory].Slots.Count(entry => entry.Item != 0), actual[inventory].OccupiedSlotCount);
                for (int index = 0; index < capacity; index++)
                {
                    Assert.Equal(expected[inventory].Slots[index].Item, actual[inventory][index].Item.Value);
                    Assert.Equal(expected[inventory].Slots[index].Quantity, actual[inventory][index].Quantity);
                }
            }
        }
    }

    private readonly record struct Entry(uint Item, int Quantity);

    // Plans modifications against copied lists, then commits only complete plans. Production
    // performs bounded preflight scans and mutates its fixed array without allocating plans.
    private sealed class Model
    {
        private readonly int[] _limits;
        public Entry[] Slots { get; private set; }

        public Model(int capacity, int[] limits)
        {
            _limits = limits;
            Slots = new Entry[capacity];
        }

        public long Quantity(uint item) => Slots.Where(entry => entry.Item == item).Sum(entry => (long)entry.Quantity);
        public long Available(uint item) => Slots.Sum(entry => entry.Item == 0 ? (long)_limits[item] :
            entry.Item == item ? _limits[item] - (long)entry.Quantity : 0);

        public bool TryAdd(uint item, long quantity)
        {
            var planned = PlanAdd(item, quantity);
            if (planned is null) return false;
            Slots = planned;
            return true;
        }

        public bool TryRemove(uint item, long quantity)
        {
            var planned = PlanRemove(item, quantity);
            if (planned is null) return false;
            Slots = planned;
            return true;
        }

        public bool TryTransferTo(Model destination, uint item, long quantity)
        {
            var removed = PlanRemove(item, quantity);
            if (removed is null) return false;
            if (ReferenceEquals(this, destination)) return true;
            var added = destination.PlanAdd(item, quantity);
            if (added is null) return false;
            Slots = removed;
            destination.Slots = added;
            return true;
        }

        public bool TryInsertAt(int index, uint item, int quantity)
        {
            var slot = Slots[index];
            if ((slot.Item != 0 && slot.Item != item) || (long)slot.Quantity + quantity > _limits[item]) return false;
            Slots[index] = new Entry(item, slot.Quantity + quantity);
            return true;
        }

        public bool TryTakeAt(int index, int quantity, out Entry taken)
        {
            var slot = Slots[index];
            if (slot.Quantity < quantity)
            {
                taken = default;
                return false;
            }
            taken = new Entry(slot.Item, quantity);
            Slots[index] = slot.Quantity == quantity ? default : new Entry(slot.Item, slot.Quantity - quantity);
            return true;
        }

        public bool TryRestore(Entry[] snapshot)
        {
            if (snapshot.Any(entry => entry.Item != 0 && entry.Quantity > _limits[entry.Item])) return false;
            Slots = (Entry[])snapshot.Clone();
            return true;
        }

        public void Clear() => Slots = new Entry[Slots.Length];

        private Entry[]? PlanAdd(uint item, long quantity)
        {
            var plan = (Entry[])Slots.Clone();
            var candidates = Enumerable.Range(0, plan.Length)
                .Where(index => plan[index].Item == item).Concat(Enumerable.Range(0, plan.Length)
                    .Where(index => plan[index].Item == 0));
            foreach (int index in candidates)
            {
                if (quantity == 0) break;
                int amount = (int)Math.Min(quantity, (long)_limits[item] - plan[index].Quantity);
                if (amount == 0) continue;
                plan[index] = new Entry(item, plan[index].Quantity + amount);
                quantity -= amount;
            }
            return quantity == 0 ? plan : null;
        }

        private Entry[]? PlanRemove(uint item, long quantity)
        {
            var plan = (Entry[])Slots.Clone();
            foreach (int index in Enumerable.Range(0, plan.Length).Where(index => plan[index].Item == item))
            {
                if (quantity == 0) break;
                int amount = (int)Math.Min(quantity, plan[index].Quantity);
                int remaining = plan[index].Quantity - amount;
                plan[index] = remaining == 0 ? default : new Entry(item, remaining);
                quantity -= amount;
            }
            return quantity == 0 ? plan : null;
        }
    }
}
