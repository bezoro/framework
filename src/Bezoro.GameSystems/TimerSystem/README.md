# TimerSystem

ECS-native timers for cooldowns and scheduled gameplay logic.

## Types

| Type                   | Description                                                                              |
|------------------------|------------------------------------------------------------------------------------------|
| `Timer`                | Timer component (duration, elapsed, state, mode, id).                                    |
| `TimerSystem`          | ECS `ISystem` that advances running timers and emits lifecycle callbacks.                |
| `TimerLifecycle`       | Transition kind: `Started`, `Paused`, `Stopped`, `Finished`, `Resumed`, `Restarted`.     |
| `TimerLifecycleEvent`  | Event payload for callbacks and event queue consumers.                                   |
| `TimerEventsResource`  | World resource queue for pull-based lifecycle event consumption.                         |
| `TimerOwner`           | Optional owner link from timer entity to gameplay entity.                                |
| `TimerWorldExtensions` | Control helpers: `StartTimer`, `PauseTimer`, `StopTimer`, `ResumeTimer`, `RestartTimer`. |

## Quick Start

```csharp
using Bezoro.ECS.Services;
using Bezoro.GameSystems.TimerSystem.Extensions;
using Bezoro.GameSystems.TimerSystem.Services;
using Bezoro.GameSystems.TimerSystem.Types;

var world = new World();
var timers = new TimerSystem();
world.AddSystem(timers);

var timerEntity = world.Spawn(new Timer(
    timerId: 1001,
    durationSeconds: 3f,
    state: TimerState.Stopped,
    mode: TimerMode.Persistent));

world.StartTimer(timerEntity);

timers.Finished += evt => Console.WriteLine($"Timer {evt.TimerId} finished.");

world.Tick(1f);
world.Tick(1f);
world.Tick(1f);

ref var eventQueue = ref world.GetResource<TimerEventsResource>();
while (eventQueue.TryDequeue(out var evt))
{
    Console.WriteLine($"{evt.Lifecycle} -> {evt.TimerId}");
}
```

## API Reference

### `Timer`

| Member             | Description                                  |
|--------------------|----------------------------------------------|
| `TimerId`          | App-level timer identifier.                  |
| `DurationSeconds`  | Total duration in seconds.                   |
| `ElapsedSeconds`   | Elapsed time in seconds.                     |
| `State`            | `Running`, `Paused`, `Stopped`, `Completed`. |
| `Mode`             | `Persistent` or `OneShot`.                   |
| `RemainingSeconds` | Clamped remaining time in seconds.           |
| `Progress`         | Completion ratio from `0` to `1`.            |

### `TimerSystem` callbacks

| Event       | Trigger                          |
|-------------|----------------------------------|
| `Started`   | Stopped timer started.           |
| `Paused`    | Running timer paused.            |
| `Stopped`   | Running or paused timer stopped. |
| `Finished`  | Running timer reaches duration.  |
| `Resumed`   | Paused timer resumed.            |
| `Restarted` | Timer reset to zero and running. |

### `TimerWorldExtensions`

| Method                 | Description                                            |
|------------------------|--------------------------------------------------------|
| `StartTimer(Entity)`   | `Stopped -> Running` and queues `Started`.             |
| `PauseTimer(Entity)`   | `Running -> Paused` and queues `Paused`.               |
| `StopTimer(Entity)`    | `Running/Paused -> Stopped` and queues `Stopped`.      |
| `ResumeTimer(Entity)`  | `Paused -> Running` and queues `Resumed`.              |
| `RestartTimer(Entity)` | `* -> Running` + elapsed reset and queues `Restarted`. |

## Design Notes

- Timers are dedicated entities, enabling many timers per gameplay object.
- `TimerSystem` writes lifecycle events to `TimerEventsResource` for pull-based consumption.
- `TimerMode.OneShot` despawns timer entities when they finish.
- Event handler exceptions are isolated and do not break system update.

## Build

```bash
dotnet build bezoro.framework.sln
dotnet test tests/Bezoro.GameSystems.Tests/Bezoro.GameSystems.Tests.csproj --filter "FullyQualifiedName~TimerSystem"
```
