# Bezoro.ECS.Tests

Unit and integration tests for `Bezoro.ECS` runtime, scheduling, source generation integration, and API contracts.

## Types

| Type                                            | Description                                                                                                                 |
|-------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------|
| `WorldRuntimeTests`                             | Shared test fixture host for private helper query specs, test-only component types, and jobs used by partial runtime tests. |
| `WorldRuntimePlaybackTests`                     | Command stream playback behavior for structural transitions, managed lanes, and cross-world guard rails.                    |
| `WorldRuntimeQueryTests`                        | Query compilation/execution behavior, cursor semantics, and typed foreach/job mutation paths.                               |
| `WorldRuntimeAccessorTests`                     | Component accessor semantics (`Has`, `TryGet`) and cache correctness across archetype transitions.                          |
| `WorldRuntimeAllocationTests`                   | Hot-path allocation assertions after warmup for accessor, cursor, foreach, run, and playback flows.                         |
| `WorldRuntimeRelationTests`                     | Relation API lifecycle and relation-filter query behavior.                                                                  |
| `WorldRuntimeLifecycleTests`                    | Entity lifecycle semantics (`Reset`, despawn/respawn versioning, `IsAlive` invariants).                                     |
| `WorldRuntimeOverflowTests`                     | Capacity and overflow policy behavior for playback and command recording.                                                   |
| `WorldApiContractTests`                         | Public API surface and contract-level behavior checks for `World`/`IWorld`.                                                 |
| `SystemManagerTests`                            | Scheduling behavior, access-conflict batching, and phase/stage execution semantics.                                         |
| `QueryGeneratorTests`                           | Compiled query usage and filtering behavior from a consumer perspective.                                                    |
| `GeneratedQueryAndJobSourceGenIntegrationTests` | Source-generated query-spec and job-extension integration against runtime execution APIs.                                   |
| `GeneratedSystemMetadataResolverTests`          | Generated system metadata discovery and resolver behavior.                                                                  |

## Quick Start

```bash
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj
```

## Design Notes

- Tests are consumer-first and validate ECS ergonomics as exposed by the public API.
- Runtime tests prioritize correctness for structural changes, deterministic playback, and fixed-capacity behavior.
- Source-generation integration tests ensure generated code stays aligned with runtime contracts.

## Test Conventions

- Method naming follows `Method_WhenCondition_ShouldExpectation`.
- Runtime coverage is split into focused partial files under `Services/WorldRuntime*Tests.cs` for maintainability.
- `World` instances are created with `using var` to keep disposal behavior explicit and consistent.
- Assertions use FluentAssertions for readable failure messages and uniform style.
