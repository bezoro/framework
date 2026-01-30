# TimerSystem

A high-performance, thread-safe timer service for managing cooldowns, timed events, and scheduled callbacks in game systems.

## Features

- **Thread-safe** -- backed by `ConcurrentDictionary` with atomic state transitions, safe for concurrent create/cancel/query from multiple threads.
- **High-precision timing** -- uses `Stopwatch` ticks internally, avoiding `DateTime` drift.
- **Configurable tick rate** -- defaults to ~60 Hz (16 ms), adjustable via `TimerConfig`.
- **Pause / Resume** -- elapsed time is preserved across pause-resume cycles.
- **Callback marshalling** -- supply a `SynchronizationContext` (e.g. Unity's main thread) to marshal callbacks off the background loop.
- **Exception-safe** -- callback and event exceptions are caught and isolated; one failing timer cannot break others.

## Quick Start

```csharp
using Bezoro.GameSystems.TimerSystem.Abstractions;
using Bezoro.GameSystems.TimerSystem.Services;
using Bezoro.GameSystems.TimerSystem.Types;

// Create the service (optionally pass TimerConfig to change tick rate)
using var timers = new TimerService();
timers.Start();

// Create a one-shot timer (auto-removed after completion — the default)
TimerHandle oneShot = timers.Create(
    TimeSpan.FromSeconds(3),
    _ => Console.WriteLine("One-shot done!"));

// Create a persistent timer (stays in storage for reuse via Restart)
TimerHandle cooldown = timers.Create(
    TimeSpan.FromSeconds(3),
    _ => Console.WriteLine("Cooldown ready!"),
    TimerMode.Persistent);

// Query progress at any time
if (timers.TryGetInfo(handle, out TimerInfo info))
{
    Console.WriteLine($"Progress: {info.Progress:P0}");   // e.g. "Progress: 45%"
    Console.WriteLine($"Remaining: {info.Remaining}");
}

// Pause, resume, restart, or cancel
timers.Pause(handle);
timers.Resume(handle);
timers.Restart(handle);
timers.Cancel(handle);

// Subscribe to completions globally
timers.TimerCompleted += args =>
    Console.WriteLine($"{args.Handle} completed");

// Clean up finished/cancelled timers from internal storage
timers.Cleanup();

// Stop the background loop and dispose
timers.Stop();
```

## API Reference

### `ITimerService`

| Member                                   | Description                                                |
|------------------------------------------|------------------------------------------------------------|
| `IsRunning`                              | Whether the background tick loop is active.                |
| `ActiveCount`                            | Number of timers in `Running` or `Paused` state.           |
| `TimerCompleted`                         | Event raised when any timer reaches its duration.          |
| `Start()`                                | Starts the background processing loop.                     |
| `Stop()`                                 | Stops the background loop.                                 |
| `Create(TimeSpan, Action?, TimerMode)`   | Creates a new timer. Returns a `TimerHandle`.              |
| `Pause(TimerHandle)`                     | Pauses a running timer, preserving elapsed time.           |
| `Resume(TimerHandle)`                    | Resumes a paused timer.                                    |
| `Restart(TimerHandle)`                   | Resets and restarts a timer from zero.                     |
| `Cancel(TimerHandle)`                    | Cancels a timer (transitions to `Stopped`).                |
| `TryGetInfo(TimerHandle, out TimerInfo)` | Queries a snapshot of timer state.                         |
| `Cleanup()`                              | Removes all `Stopped` and `Completed` timers from storage. |

### `TimerHandle`

Lightweight `readonly record struct` used to identify a timer. Compare against `TimerHandle.None` or check `IsValid` to detect invalid handles.

### `TimerInfo`

Read-only snapshot returned by `TryGetInfo`:

| Property    | Type          | Description                                     |
|-------------|---------------|-------------------------------------------------|
| `Handle`    | `TimerHandle` | The timer's handle.                             |
| `State`     | `TimerState`  | `Running`, `Paused`, `Stopped`, or `Completed`. |
| `Mode`      | `TimerMode`   | `OneShot` or `Persistent`.                      |
| `Duration`  | `TimeSpan`    | Total configured duration.                      |
| `Elapsed`   | `TimeSpan`    | Time elapsed so far.                            |
| `Remaining` | `TimeSpan`    | Time left (clamped to zero).                    |
| `Progress`  | `double`      | `0.0` to `1.0` completion ratio.                |

### `TimerConfig`

| Property          | Default | Description                                         |
|-------------------|---------|-----------------------------------------------------|
| `TickRateMs`      | `16`    | Milliseconds between tick iterations (~60 Hz).      |
| `CallbackContext` | `null`  | `SynchronizationContext` for marshalling callbacks. |

### `TimerMode`

| Value        | Description                                                                                     |
|--------------|-------------------------------------------------------------------------------------------------|
| `OneShot`    | Default. Automatically removed from storage after the completion callback fires.                |
| `Persistent` | Stays in storage after completion, allowing reuse via `Restart()` (e.g. skill cooldowns).       |

> **Note:** `Cleanup()` removes all `Completed` and `Stopped` timers regardless of mode.

### `TimerState`

```
Running  -->  Paused  -->  Running  (pause/resume cycle)
Running  -->  Completed              (duration reached)
Running  -->  Stopped                (cancelled)
Paused   -->  Stopped                (cancelled while paused)
Stopped / Completed  -->  Running    (restart)
```

## Unity Integration

Pass Unity's synchronization context so callbacks run on the main thread:

```csharp
var config = new TimerConfig
{
    CallbackContext = SynchronizationContext.Current  // capture on main thread
};

using var timers = new TimerService(config);
timers.Start();

// Callbacks will be posted to Unity's main thread
timers.Create(TimeSpan.FromSeconds(1), () =>
{
    // Safe to access Unity APIs here
    Debug.Log("Timer done!");
});
```

## Thread Safety

All public methods on `TimerService` are safe to call from any thread. State transitions use `ConcurrentDictionary.AddOrUpdate` for atomicity. The background tick loop runs on a `Task.Run` thread pool thread and respects `Stop()` / `Dispose()` signals.

## Running Tests

```bash
dotnet test tests/Bezoro.GameSystems.Tests/Bezoro.GameSystems.Tests.csproj --filter "FullyQualifiedName~TimerSystem"
```

Test coverage includes state transitions, pause/resume timing, concurrent access, callback exception safety, disposal semantics, and restart behavior.
