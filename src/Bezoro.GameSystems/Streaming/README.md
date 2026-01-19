# Streaming System

A thread-safe, distance-based entity streaming system for managing entity visibility and activation based on proximity to a reference point.

## Use Cases

- **Game World Streaming**: Stream game entities in/out based on player proximity
- **LOD Systems**: Activate detailed entities only when players are nearby
- **Performance Optimization**: Reduce active entity count by disabling distant objects
- **Open World Games**: Manage large numbers of entities across expansive game worlds

## Features

- **Distance-based activation** - Entities stream in when within a configurable distance, stream out when beyond another
- **Hysteresis support** - Separate in/out distances prevent flickering at boundary edges
- **Thread-safe processing** - Background thread evaluates distances without blocking the main thread
- **Round-robin load distribution** - Processes a configurable number of entities per iteration to spread CPU load
- **Main thread callbacks** - Uses `SynchronizationContext` to marshal `OnStreamIn`/`OnStreamOut` calls to the main thread

## Quick Start

```csharp
// 1. Implement IStreamableEntity on your entities
public class GameEntity : IStreamableEntity
{
    public int EntityId { get; }
    public Vector3 StreamingPosition => transform.Position;
    public bool IsStreamedIn { get; private set; }

    public void OnStreamIn()
    {
        IsStreamedIn = true;
        // Enable visuals, physics, AI, etc.
    }

    public void OnStreamOut()
    {
        IsStreamedIn = false;
        // Disable or unload entity resources
    }
}

// 2. Create and configure the streaming system
using var streamingSystem = new StreamingSystem();

// 3. Register entities
foreach (var entity in entities)
    streamingSystem.Register(entity);

// 4. Start with configuration
streamingSystem.Start(new StreamingConfig
{
    GetReferencePosition = () => player.Position,
    StreamInDistance = 100f,
    StreamOutDistance = 120f,  // Hysteresis buffer
    MaxPerFrame = 50,
    FrameDelayMs = 16
});

// 5. Stop when done (or dispose)
streamingSystem.Stop();
```

## API Reference

### IStreamableEntity

Interface that entities must implement to participate in streaming.

| Member              | Type      | Description                                        |
|---------------------|-----------|----------------------------------------------------|
| `EntityId`          | `int`     | Unique identifier for the entity                   |
| `StreamingPosition` | `Vector3` | Position used for distance calculations            |
| `IsStreamedIn`      | `bool`    | Current streaming state                            |
| `OnStreamIn()`      | `void`    | Called when entity should activate (main thread)   |
| `OnStreamOut()`     | `void`    | Called when entity should deactivate (main thread) |

### StreamingConfig

Configuration struct for the streaming system.

| Field                  | Type            | Description                                                    |
|------------------------|-----------------|----------------------------------------------------------------|
| `GetReferencePosition` | `Func<Vector3>` | Delegate returning the reference point (e.g., player position) |
| `StreamInDistance`     | `float`         | Distance threshold for streaming in                            |
| `StreamOutDistance`    | `float`         | Distance threshold for streaming out                           |
| `MaxPerFrame`          | `int`           | Maximum entities to process per iteration                      |
| `FrameDelayMs`         | `int`           | Delay between processing iterations                            |

### StreamingSystem

Main class that coordinates entity streaming.

| Member               | Description                                              |
|----------------------|----------------------------------------------------------|
| `IsRunning`          | Whether the background processing loop is active         |
| `EntityCount`        | Number of currently registered entities                  |
| `Register(entity)`   | Add an entity to the streaming system                    |
| `Unregister(entity)` | Remove an entity from the streaming system               |
| `Start(config)`      | Begin background processing with the given configuration |
| `Stop()`             | Stop background processing and clear pending operations  |
| `Dispose()`          | Stop and release all resources                           |

## Configuration

| Option              | Recommended Value                 | Notes                                                                 |
|---------------------|-----------------------------------|-----------------------------------------------------------------------|
| `StreamInDistance`  | Game-dependent                    | Set based on your entity visibility requirements                      |
| `StreamOutDistance` | `StreamInDistance * 1.1` to `1.3` | Should be greater than `StreamInDistance` to create hysteresis buffer |
| `MaxPerFrame`       | 50-200                            | Balance between responsiveness and CPU usage                          |
| `FrameDelayMs`      | 16-33                             | Roughly 30-60 updates per second; increase for lower CPU usage        |

### Hysteresis Explained

The separate `StreamInDistance` and `StreamOutDistance` values prevent "flickering" when an entity is right at the boundary:

```
            StreamInDistance    StreamOutDistance
                  |                    |
    [Far Away]    |   [Active Zone]    |   [Buffer Zone]
                  |                    |
  <- Streams Out  |  <- Stays Active ->|  -> Stays Out
```

Without hysteresis, an entity exactly at the threshold would rapidly toggle between streamed-in and streamed-out states.

## Threading Model

```
┌─────────────────────────────────────────────────────────────┐
│                    Background Thread                        │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  ProcessingLoop:                                      │  │
│  │    1. Get reference position                          │  │
│  │    2. Round-robin through entities (up to MaxPerFrame)│  │
│  │    3. Calculate squared distances                     │  │
│  │    4. Queue streaming results                         │  │
│  │    5. Sleep for FrameDelayMs                          │  │
│  └───────────────────────────────────────────────────────┘  │
│                           │                                 │
│                           ▼                                 │
│            SynchronizationContext.Post()                    │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌────────────────────────────────────────────────────────────┐
│                      Main Thread                           │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  ApplyResults:                                      │   │
│  │    - Invoke OnStreamIn() / OnStreamOut()            │   │
│  └─────────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────────┘
```

### Thread Safety Guarantees

- **Registration/Unregistration**: Safe to call from any thread
- **Start/Stop**: Should be called from the main thread
- **Callbacks**: Always invoked on the main thread (via `SynchronizationContext`)
- **Entity access**: Uses `ConcurrentDictionary` for thread-safe entity storage

## Implementation Notes

### Squared Distance Optimization

Distance calculations use squared distances to avoid expensive `sqrt()` operations:

```csharp
float distSq = DistanceSquared(referencePos, entity.StreamingPosition);
if (distSq <= _inDistanceSquared) // Pre-computed: StreamInDistance * StreamInDistance
```

### Round-Robin Processing

Entities are processed in round-robin fashion using a persistent index. This ensures:
- All entities get evaluated over time
- No entity is starved of updates
- Processing load is distributed evenly

### Exception Handling

The system is designed to be resilient:
- Exceptions in `GetReferencePosition` skip the current iteration
- Exceptions in entity callbacks are caught and don't crash the processing loop
- Cancellation is handled gracefully during shutdown

### Memory Considerations

- Entity keys are snapshotted each iteration to handle concurrent modifications
- Results are queued and processed in batches
- The system cleans up pending results on `Stop()`
