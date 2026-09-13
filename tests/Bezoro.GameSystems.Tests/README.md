# Bezoro.GameSystems.Tests

Behavior and contract tests for the ECS-native gameplay systems in `Bezoro.GameSystems`.

## Test Areas

- Activation command ordering, cancellation, capacity, dispatch, handles, and world registration.
- Health mutation, clamping, lifecycle, event publication, and system scheduling.
- Input ingestion, movement intent, velocity updates, and command buffering.
- Movement-system position integration.
- Streaming transitions, capacity, event publication, and runtime state.
- Timer modes, lifecycle transitions, ownership, events, and world extensions.

## Quick Start

```powershell
dotnet test tests/Bezoro.GameSystems.Tests/Bezoro.GameSystems.Tests.csproj
```

## What These Tests Guarantee

- Public gameplay-system APIs compose with a real `World`.
- Queue boundaries preserve documented ordering and capacity behavior.
- Generated ECS query specifications compile and execute against their intended components.
- Observable component, resource, and lifecycle-event state matches each subsystem contract.

## Conventions

- Tests use xUnit and FluentAssertions.
- Test classes mirror the source subsystem folders.
- Tests exercise public behavior and world state rather than private implementation calls.
- Each test creates isolated world and queue state.

## Design Notes

- These are unit-level composition tests; they do not require Unity or another engine.
- Concurrency tests coordinate work with bounded synchronization rather than timing-only assertions.
