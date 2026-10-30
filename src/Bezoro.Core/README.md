# Bezoro.Core

Engine-independent collections, grids, object pools, value types, results, and guard extensions shared by the framework. The library targets .NET 10 and .NET Standard 2.1. Unity 6 consumes the precompiled .NET Standard DLLs; its project compiler does not need to compile the framework's source syntax.

## Object pools

Supply a reset policy when rented objects carry mutable state:

```csharp
using System.Text;
using Bezoro.Core.Types;
using Bezoro.Core.Types.Pool;

var policy = new PoolPolicy<StringBuilder>(
    factory: () => new StringBuilder(),
    reset: builder => { builder.Clear(); return true; });

using var pool = new ObjectPool<StringBuilder>(policy, new PoolOptions(
    InitialCapacity: 16,
    MaxCapacity: 128,
    EnableAsyncWait: true,
    TrackStatistics: true));

using (var handle = pool.RentHandle())
{
    handle.Value.Append("Temporary text");
}

// The handle has returned the cleared builder to the pool.
var statistics = pool.Statistics;
```

`Rent()` reuses an available object or creates one within capacity. `TryRent()` only takes an available object; it does not create one. Exhausted synchronous rents throw `PoolExhaustedException`. Async rents can wait when `EnableAsyncWait` is enabled and capacity is bounded:

```csharp
var item = await pool.RentAsync(TimeSpan.FromSeconds(1), cancellationToken);
if (item != null)
{
    try { item.Append("Work"); }
    finally { pool.Return(item); }
}
```

- The timed overload returns `null` on timeout. `Timeout.InfiniteTimeSpan` waits without a time limit. `RentAsync(CancellationToken)` also waits without a time limit.
- Cancellation throws `OperationCanceledException`. Disposing the pool completes pending waits with `ObjectDisposedException`.
- Returning the same available object again returns `false` without running its reset policy again. Return processing claims ownership before invoking reset, including when accepting a foreign object. Concurrent duplicate returns of the claimed object run reset at most once. A stale raw reference is still invalid after the object is rented again; callers must not return or mutate it.
- A foreign object can be accepted while the live pool has free capacity. A full pool discards the foreign object and keeps ownership of its existing rentals. Returning an unknown object after disposal returns `false` without discarding it; its owner remains responsible for cleanup.
- A rejected or throwing reset, or throwing rent validation, removes and discards the affected owned object. Cleanup releases capacity. Exceptions from reset or validation remain visible to the caller.
- Failed prewarming releases objects already created. Clearing available objects attempts cleanup of every object even when one cleanup callback throws, then rethrows the first failure.
- `Clear()` disposes available `IDisposable` values directly. `ClearWithPolicyDiscard()` uses the policy's discard callback. `Dispose()` clears available objects; outstanding rentals are discarded when returned afterward. Return rentals before disposing their pool when possible.
- Copies of `PooledObjectHandle<T>` share disposal state and return their value once. `RentHandle()` allocates that shared state. Raw `Rent()`/`Return()` avoids the handle allocation but requires explicit ownership discipline.

Pool bookkeeping is thread-safe. Rented objects are not made thread-safe by the pool, and policy callbacks may run concurrently for different objects. A single rental should have one owner. Statistics are concurrent observations rather than an atomic snapshot of every counter.

`PoolOptions.ShrinkThreshold` and `AsyncWaitTimeout` currently do not trigger automatic trimming or a default timeout. Call `TrimExcess(...)` and select a timed rent explicitly. Statistics counters require `TrackStatistics = true`.

## Collections

```csharp
using Bezoro.Core.Types;

var entityIds = new SwapbackArray<int> { 10, 20, 30 };
if (entityIds.TryGetIndex(20, out uint index))
    entityIds.TryRemoveAt(index);

entityIds.ClearRetainingCapacity(); // Reuse the backing allocation.
entityIds.Clear();                  // Clear and shrink to minimum capacity.
```

`SwapbackArray<T>` removes an item by moving the last item into its slot. Indexed removal is O(1) before any optional shrink; searching by value is O(n), and shrinking copies retained values. Set `ShrinkThresholdPercent = Percent.Zero` to defer shrinking until the collection is empty, or reuse capacity with `ClearRetainingCapacity()` between workloads.

`ReplaceAt` and the indexer require `index < Count`. `TryGetIndex` returns `false` with index zero when absent. Adding the collection directly to itself appends its current contents once. An enumerable that reads the collection indirectly, such as a LINQ query over it, is invalidated when appending starts and throws instead of growing indefinitely. A failed enumeration can leave successfully appended items in the destination and invalidates existing enumerators.

These collections require external synchronization for concurrent mutation and access. Spans returned by `AsSpan()` or `AsMutableSpanUnsafe()` borrow storage and must not survive operations that resize it. Mutating the unsafe span bypasses enumeration version tracking.

## Grids and borrowed views

For fixed-capacity gameplay storage, see [generational slot maps and ring buffers](Collections/README.md). For replayable procedural generation, shuffling, and weighted selection, see [deterministic random streams](Random/README.md). Their capacities and state ownership are explicit; they do not introduce global random state or synchronize mutable game data implicitly.

```csharp
using Bezoro.Core.Types;

using var grid = new Grid2D<int>(width: 8, height: 8, usePooling: true);
grid[2, 3] = 5;
var row = grid.GetRow(3);
var view = grid.AsGridSpan();
```

Grids use row-major storage. Dimensions must be positive, and dimension multiplication is checked for integer overflow. `GridSpan2D<T>` and `ReadOnlyGridSpan2D<T>` require enough backing elements for the complete grid. Indexers validate each coordinate independently, so an invalid column cannot alias another row.

A newly rented grid initializes its logical cells to `default(T)`. The default-value constructor fills those cells with the supplied value. Pooling reuses the backing array; it does not remove the cost of initializing or clearing elements.

`Dispose()` is idempotent and returns a pooled backing array once. Subsequent storage access through the owning grid throws `ObjectDisposedException`. Existing spans, rows, and refs cannot enforce that lifetime: stop using all borrowed views before disposal. Coordinate queries such as `IsInBounds` only inspect dimensions and do not establish that storage is alive. Synchronize grid access and disposal externally.

## Results and guards

```csharp
using Bezoro.Core.Types;

var result = ParseNumber("42");
var (ok, value) = Try.TryGet(() => int.Parse("42"));

static Result<int> ParseNumber(string input) =>
    int.TryParse(input, out var value)
        ? ResultFactory.Succeeded(value)
        : ResultFactory.Failed<int>(new ParseFailure(input));

sealed record ParseFailure(string Input) : IFailureReason;
```

Prefer direct guards or focused extensions such as `ThrowIfNull` and `ThrowIfEmpty` in new code. The obsolete `ValidationHelper.ThrowIfObjectIsNull` now follows its documented contract: a non-null value passes, and a null value throws with the supplied parameter name and details.

## Other APIs

| API | Purpose |
| --- | --- |
| `Percent`, `Color`, `UIntVector2`, `UIntRange` | Common value types and their formatting helpers. |
| `Singleton<T>` | Explicit configure, recreate, reset, and dispose operations for a shared instance. |
| `SingleItemEnumerable<T>` | Enumerates one value. |
| String formatting extensions | Rich-text formatting with `Color(string)` and `Color(Color)`. |
| `CodeWriter`, `CSharpCodeBuilder` | Builds generated C# source. |

Format enum values with `ToString()` and then the string extensions. `ColorHex`, parameterless named-color methods, and enum styling wrappers remain obsolete compatibility APIs.

## Build and validation

Use the SDK selected by [global.json](../../global.json):

```bash
dotnet build src/Bezoro.Core/Bezoro.Core.csproj -c Release
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj -c Release -f net10.0
```

The first command builds both runtime targets. See [the Core tests](../../tests/Bezoro.Core.Tests/README.md) for covered contracts.
