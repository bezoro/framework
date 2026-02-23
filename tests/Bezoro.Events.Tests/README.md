# Bezoro.Events.Tests
Unit tests for `Bezoro.Events` covering event bus behavior, queueing semantics, chaining, cancellation, and core event types.

## Test Areas
| Folder                     | Source Mirror                                   | Description                                                                     |
|----------------------------|-------------------------------------------------|---------------------------------------------------------------------------------|
| `Services/EventBus`        | `src/Bezoro.Events/Services/EventBus.cs`        | Publish/subscribe, priority ordering, queueing, disposal, concurrency, chaining |
| `Services/UnityEventBuses` | `src/Bezoro.Events/Services/UnityEventBuses.cs` | Unity loop-specific queue flushing and disposal behavior                        |
| `Types`                    | `src/Bezoro.Events/Types/*`                     | `EventContext<T>` and `SubscriptionHandle` behavior                             |
| `Services/Fixtures`        | Test-only                                       | Shared test event records used across service/type tests                        |

## Quick Start
```bash
dotnet test tests/Bezoro.Events.Tests/Bezoro.Events.Tests.csproj
```

## Conventions
- Test class naming: `{TypeName}Tests`
- Test method naming: `Method_WhenCondition_ShouldExpectation`
- One behavior per test with explicit Arrange/Act/Assert flow
- Fixtures and shared test-only types live under `Services/Fixtures`
