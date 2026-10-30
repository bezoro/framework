# Compact flags and span helpers

`BitSet` stores a fixed number of flags in a `ulong[]`. Backing storage uses `8 × ceil(Length / 64)` bytes, plus the array and owner headers. Construction allocates that storage once. Zero length is valid. Unused bits in the last word stay clear.

```csharp
using Bezoro.Core.Collections;

var unlocked = new BitSet(1024);
unlocked.Set(17);
unlocked[512] = true;
unlocked.Reset(17);

foreach (int abilityId in unlocked)
    EnableAbility(abilityId);

int next = unlocked.FindNextSetBit(startIndex: 100); // 512, or -1 when absent.
```

`Length` is the number of addressable flags. `Count` scans words and returns the number of set flags. Indexing, setting, clearing one flag, and toggling take O(1) time. `Set` and `Reset` return whether the value changed; `Toggle` returns the new flag value.

The .NET target uses `BitOperations` for population count and bit scans. The .NET Standard target uses portable integer implementations and requires no processor-specific intrinsics.

`UnionWith`, `IntersectWith`, `ExceptWith`, and `XorWith` operate in place in O(ceil(Length / 64)) time. Both operands must have equal logical lengths, even when they would occupy the same number of words. Null or mismatched operands throw before changing either set. Self operations are supported. `SetAll` and `Clear` retain capacity.

The struct enumerator visits set indices in ascending order without boxing. Changes to flag contents invalidate existing enumerators; no-op operations do not. Enumeration scans each word once and each set bit once. `FindNextSetBit` starts at an inclusive index between zero and `Length`; `Length` returns -1. Negative indices and indices greater than `Length` throw.

Concurrent reads are supported while no thread mutates the set. Coordinate writes and readers externally. Flags are not atomic, and the owner creates no hidden locks or tasks.

## Sorted spans

```csharp
using Bezoro.Core.Extensions;

ReadOnlySpan<int> costs = new[] { 2, 4, 4, 9 };
int first = costs.LowerBound(4); // 1
int end = costs.UpperBound(4);   // 3
ReadOnlySpan<int> equalCosts = costs.Slice(first, end - first);
```

`LowerBound` finds the first element comparing greater than or equal to the value. `UpperBound` finds the first element comparing strictly greater. An absent bound returns the span length, including zero for empty spans. Both use binary search and O(log Length) comparisons. They work on `Span<T>` and `ReadOnlySpan<T>` without changing storage.

The caller must provide data already sorted with the same comparer. Null uses `Comparer<T>.Default`; a custom comparer can define descending order or string comparison policy. Search allocates no storage after comparer initialization. A caller-supplied comparer can allocate or throw, and the helpers do not change its behavior.

## Removing from a logical span

```csharp
Span<int> live = new[] { 10, 20, 30, 40 };
live = live.RemoveAtOrdered(1);  // 10, 30, 40
live = live.RemoveAtSwapBack(0); // 40, 30
```

Keep the returned span: its length is the updated logical element count. `RemoveAtOrdered` shifts successors left in O(Length - index) time. `RemoveAtSwapBack` moves the last element into the removed position in O(1) time and may change order. Both clear the old last element to `default(T)` so the backing storage releases removed references.

The methods validate the index before mutation. They affect only the supplied slice, preserve adjacent backing elements, and allocate no replacement storage. Aliases observe the mutation; the backing array itself retains its original length. Stop using borrowed spans before their owner's storage is resized, returned to a pool, or disposed.

The [Core tests](../../../tests/Bezoro.Core.Tests/README.md) include seeded boolean/list/linear-search reference models and warmed allocation checks. `BufferUtilitiesBenchmarks` measures individual workloads with reusable storage. It does not establish Unity player or IL2CPP timings.
