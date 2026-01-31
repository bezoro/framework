# ActivationSystem

A batch activation service that spreads object activation over time using a time-budget approach. Runs on a background thread, dynamically determines batch sizes to avoid frame spikes, and marshals callbacks to a specified `SynchronizationContext` (e.g. Unity main thread).

## Features

- **Time-budget batching** -- each iteration activates items within a configurable millisecond budget, preventing frame spikes.
- **Priority ordering** -- higher-priority items are activated first; equal priorities are processed in registration order.
- **Thread-safe** -- backed by `ConcurrentDictionary` with atomic state transitions, safe for concurrent register/cancel/query from multiple threads.
- **Callback marshalling** -- supply a `SynchronizationContext` (e.g. Unity's main thread) to marshal callbacks off the background loop.
- **Dynamic registration** -- items can be registered before or after `Start()`; new registrations are picked up on the next iteration.
- **Exception-safe** -- callback and event exceptions are caught and isolated; one failing callback cannot break others.

## Quick Start

```csharp
using Bezoro.GameSystems.ActivationSystem.Abstractions;
using Bezoro.GameSystems.ActivationSystem.Services;
using Bezoro.GameSystems.ActivationSystem.Types;

using var activator = new ActivationService();

// Register items to activate (can be done before or after Start)
ActivationHandle h1 = activator.Register(() => LoadChunk(0, 0), priority: 10);
ActivationHandle h2 = activator.Register(() => LoadChunk(1, 0), priority: 5);
ActivationHandle h3 = activator.Register(() => LoadChunk(0, 1), priority: 5);

// Start the background processing loop
activator.Start(new ActivationConfig(
    timeBudgetMs:     2.0,    // spend at most 2ms per iteration
    iterationDelayMs: 16,     // ~60 Hz
    minBatchSize:     1,      // always activate at least 1 item
    maxBatchSize:     50      // never activate more than 50 per iteration
));

// Subscribe to completion
activator.Completed += () => Console.WriteLine("All items activated!");

// Cancel an item before it gets activated
activator.Cancel(h3);

// Query progress
Console.WriteLine($"Pending: {activator.PendingCount}");
Console.WriteLine($"Activated: {activator.ActivatedCount}");
Console.WriteLine($"Complete: {activator.IsComplete}");

// Stop the background loop
activator.Stop();
```

## API Reference

### `IActivationService`

| Member                              | Description                                                    |
|-------------------------------------|----------------------------------------------------------------|
| `IsRunning`                         | Whether the background processing loop is active.              |
| `IsComplete`                        | Whether all pending items have been activated.                 |
| `PendingCount`                      | Number of entries waiting to be activated.                     |
| `ActivatedCount`                    | Number of entries that have been activated.                    |
| `Completed`                         | Event raised when all pending items finish activating.         |
| `Register(Action, int)`             | Registers a callback with optional priority. Returns a handle. |
| `Cancel(ActivationHandle)`          | Cancels a pending entry before activation.                     |
| `Start(ActivationConfig)`           | Starts the background processing loop.                         |
| `Stop()`                            | Stops the background processing loop.                          |

### `ActivationHandle`

Lightweight `readonly struct` used to identify an activation entry. Compare against `ActivationHandle.None` or check `IsValid` to detect invalid handles.

### `ActivationConfig`

| Property           | Default        | Description                                                     |
|--------------------|----------------|-----------------------------------------------------------------|
| `TimeBudgetMs`     | `2.0`          | Maximum milliseconds to spend activating per iteration.         |
| `IterationDelayMs` | `16`           | Delay between iterations (~60 Hz).                              |
| `MinBatchSize`     | `1`            | Always activate at least this many items, regardless of budget. |
| `MaxBatchSize`     | `int.MaxValue` | Cap on items activated per iteration.                           |
| `CallbackContext`  | `null`         | `SynchronizationContext` for marshalling callbacks.             |

### `ActivationState`

```
Pending  -->  Activated   (processed by background loop)
Pending  -->  Cancelled   (cancelled by user)
```

## Unity Integration

Pass Unity's synchronization context so callbacks run on the main thread:

```csharp
var config = new ActivationConfig(
    timeBudgetMs:    2.0,
    callbackContext: SynchronizationContext.Current  // capture on main thread
);

using var activator = new ActivationService();
activator.Start(config);

// Callbacks will be posted to Unity's main thread
activator.Register(() =>
{
    // Safe to access Unity APIs here
    myGameObject.SetActive(true);
}, priority: 10);
```

## Thread Safety

All public methods on `ActivationService` are safe to call from any thread. State transitions use `ConcurrentDictionary.AddOrUpdate` for atomicity. The background loop runs on a `Task.Run` thread pool thread and respects `Stop()` / `Dispose()` signals.

## Running Tests

```bash
dotnet test tests/Bezoro.GameSystems.Tests/Bezoro.GameSystems.Tests.csproj --filter "FullyQualifiedName~ActivationSystem"
```

Test coverage includes registration, priority ordering, cancellation, time-budget adherence, batch size limits, callback marshalling, concurrent access, completion events, and disposal semantics.
