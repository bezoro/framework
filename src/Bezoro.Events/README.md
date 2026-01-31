# Events

A type-safe event bus with priority-ordered handlers, cancellable propagation, and dual dispatch modes (synchronous inline + queued).

## Types

| Type                    | Description                                                        |
|-------------------------|--------------------------------------------------------------------|
| `IEventBus`             | Core event bus contract (subscribe, publish, enqueue, flush)       |
| `EventBus`              | Thread-safe sealed implementation                                  |
| `EventContext<TEvent>`  | Context passed to handlers, carries `Data` and `Handled` flag      |
| `SubscriptionHandle`    | Lightweight value-type handle identifying a subscription           |

## Quick Start

```csharp
using Bezoro.Events.Services;
using Bezoro.Events.Types;

// Define events as readonly record structs
public readonly record struct EnemyDiedEvent(int EnemyId, int ExperienceValue);
public readonly record struct GiveExperienceEvent(int Amount);
public readonly record struct LevelUpEvent(int NewLevel);

using var bus = new EventBus();

// Subscribe with priority (higher runs first)
bus.Subscribe<EnemyDiedEvent>(ctx =>
    bus.Publish(new GiveExperienceEvent(ctx.Data.ExperienceValue)));

bus.Subscribe<GiveExperienceEvent>(ctx =>
{
    player.Exp += ctx.Data.Amount;
    if (player.Exp >= player.ExpToNextLevel)
        bus.Publish(new LevelUpEvent(player.Level + 1));
});

bus.Subscribe<LevelUpEvent>(ctx => player.Level = ctx.Data.NewLevel);

// Publish inline (handlers execute synchronously, chaining works)
bus.Publish(new EnemyDiedEvent(42, 150));
```

## API Reference

### IEventBus

| Member                                                          | Description                                          |
|-----------------------------------------------------------------|------------------------------------------------------|
| `Subscribe<TEvent>(Action<EventContext<TEvent>>, int priority)` | Subscribe a handler; returns `SubscriptionHandle`    |
| `Unsubscribe(SubscriptionHandle)`                               | Remove a subscription; returns `true` if found       |
| `Publish<TEvent>(TEvent)`                                       | Dispatch event inline to all handlers                |
| `Enqueue<TEvent>(TEvent)`                                       | Queue event for deferred dispatch                    |
| `FlushQueued()`                                                 | Dispatch all queued events; returns count dispatched |
| `int QueuedCount`                                               | Number of events currently in the queue              |
| `int SubscriptionCount`                                         | Total active subscriptions across all event types    |

### EventContext\<TEvent\>

| Member         | Description                                           |
|----------------|-------------------------------------------------------|
| `TEvent Data`  | The event data                                        |
| `bool Handled` | Set to `true` to stop further handlers from executing |

### SubscriptionHandle

| Member              | Description                                 |
|---------------------|---------------------------------------------|
| `int Id`            | Unique subscription identifier              |
| `bool IsValid`      | `true` when `Id > 0`                        |
| `static None`       | Represents an invalid/uninitialized handle  |

## Priority & Cancellation

```csharp
// Higher priority runs first
bus.Subscribe<DamageEvent>(ctx =>
{
    // Shield absorbs damage, stop propagation
    ctx.Handled = true;
}, priority: 100);

bus.Subscribe<DamageEvent>(ctx =>
{
    // This won't run if the shield handler set Handled = true
    health.Current -= ctx.Data.Amount;
}, priority: 0);
```

## Queued Dispatch

```csharp
// Enqueue events during gameplay
bus.Enqueue(new DamageEvent(25));
bus.Enqueue(new HealEvent(10));

// Flush at a controlled point in the game loop
int dispatched = bus.FlushQueued(); // returns 2
```

## Unity Example — Enemy Kill Chain

A full example showing how decoupled systems communicate through event chaining in a Unity game. Killing an enemy triggers experience gain, which may trigger a level-up — each system only knows about its own event.

### Events

```csharp
// Events.cs
public readonly record struct EnemyKilledEvent(int EnemyId, int ExperienceReward);
public readonly record struct ExperienceGainedEvent(int Amount, int NewTotal);
public readonly record struct LevelUpEvent(int OldLevel, int NewLevel);
```

### Game Manager (wires up the bus)

```csharp
// GameManager.cs
using Bezoro.Events.Abstractions;
using Bezoro.Events.Services;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static IEventBus Bus { get; private set; }

    void Awake() => Bus = new EventBus();
    void OnDestroy() => Bus?.Dispose();
}
```

### Combat System (publishes kills)

```csharp
// CombatSystem.cs
using UnityEngine;

public class CombatSystem : MonoBehaviour
{
    public void OnEnemyDefeated(int enemyId, int expReward)
    {
        GameManager.Bus.Publish(new EnemyKilledEvent(enemyId, expReward));
    }
}
```

### Experience System (listens for kills, publishes experience + level-ups)

```csharp
// ExperienceSystem.cs
using Bezoro.Events.Types;
using UnityEngine;

public class ExperienceSystem : MonoBehaviour
{
    [SerializeField] int currentExp;
    [SerializeField] int level = 1;
    [SerializeField] int expPerLevel = 100;

    SubscriptionHandle _handle;

    void OnEnable()
    {
        _handle = GameManager.Bus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
    }

    void OnDisable()
    {
        GameManager.Bus.Unsubscribe(_handle);
    }

    void OnEnemyKilled(EventContext<EnemyKilledEvent> ctx)
    {
        currentExp += ctx.Data.ExperienceReward;

        GameManager.Bus.Publish(new ExperienceGainedEvent(
            ctx.Data.ExperienceReward, currentExp));

        while (currentExp >= expPerLevel)
        {
            currentExp -= expPerLevel;
            var oldLevel = level;
            level++;

            // Chains inline — UI and VFX handlers fire immediately
            GameManager.Bus.Publish(new LevelUpEvent(oldLevel, level));
        }
    }
}
```

### UI System (listens for experience and level-ups)

```csharp
// UISystem.cs
using Bezoro.Events.Types;
using UnityEngine;
using UnityEngine.UI;

public class UISystem : MonoBehaviour
{
    [SerializeField] Text expLabel;
    [SerializeField] Text levelLabel;
    [SerializeField] GameObject levelUpBanner;

    SubscriptionHandle _expHandle;
    SubscriptionHandle _levelHandle;

    void OnEnable()
    {
        _expHandle   = GameManager.Bus.Subscribe<ExperienceGainedEvent>(OnExpGained);
        _levelHandle = GameManager.Bus.Subscribe<LevelUpEvent>(OnLevelUp);
    }

    void OnDisable()
    {
        GameManager.Bus.Unsubscribe(_expHandle);
        GameManager.Bus.Unsubscribe(_levelHandle);
    }

    void OnExpGained(EventContext<ExperienceGainedEvent> ctx)
    {
        expLabel.text = $"EXP: {ctx.Data.NewTotal}";
    }

    void OnLevelUp(EventContext<LevelUpEvent> ctx)
    {
        levelLabel.text = $"Level {ctx.Data.NewLevel}";
        levelUpBanner.SetActive(true);
    }
}
```

### VFX System (level-up particles with priority)

```csharp
// VFXSystem.cs
using Bezoro.Events.Types;
using UnityEngine;

public class VFXSystem : MonoBehaviour
{
    [SerializeField] ParticleSystem levelUpParticles;

    SubscriptionHandle _handle;

    void OnEnable()
    {
        // High priority — VFX fires before UI updates
        _handle = GameManager.Bus.Subscribe<LevelUpEvent>(OnLevelUp, priority: 10);
    }

    void OnDisable()
    {
        GameManager.Bus.Unsubscribe(_handle);
    }

    void OnLevelUp(EventContext<LevelUpEvent> ctx)
    {
        levelUpParticles.Play();
    }
}
```

### What happens when an enemy dies

```
CombatSystem.OnEnemyDefeated()
  → Publish(EnemyKilledEvent)
    → ExperienceSystem.OnEnemyKilled()
        → Publish(ExperienceGainedEvent)
            → UISystem.OnExpGained()          // updates EXP label
        → Publish(LevelUpEvent)               // if threshold met
            → VFXSystem.OnLevelUp()           // priority 10, runs first
            → UISystem.OnLevelUp()            // priority 0, runs second
```

All of this happens synchronously within a single `Publish` call. Each system is fully decoupled — `CombatSystem` has no reference to `ExperienceSystem`, which has no reference to `UISystem` or `VFXSystem`.

## Design Notes

- **Delegate handlers**: Handlers are `Action<EventContext<TEvent>>` delegates, not interface-based. Class-based handlers can pass `handler.Handle` as the delegate.
- **No marker interface**: Events are constrained by `where TEvent : struct` only, avoiding boxing overhead from interface dispatch.
- **Snapshot-copy dispatch**: Handler lists are copied before iteration, making it safe for handlers to subscribe, unsubscribe, or publish new events during handling.
- **Exception isolation**: Each handler invocation is wrapped in try-catch. A throwing handler does not prevent subsequent handlers from executing.
- **Thread safety**: `ConcurrentDictionary` for type-to-handler mapping, `lock` on individual handler lists for mutation, `ConcurrentQueue` for the event queue, `Interlocked.Increment` for ID generation.
- **Priority ordering**: Handlers are sorted descending by priority at insertion time. Same-priority handlers run in subscription order (stable).
