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
| `WorldApiContractTests`                         | Public API contracts for `World`/`IWorld`, canonical `SystemContext.CommandStream`, compatibility identity, exact obsolete metadata, and legacy world-options adapter behavior. |
| `SystemManagerTests`                            | Scheduling behavior, access-conflict batching, and phase/stage execution semantics.                                         |
| `SystemManagerErgonomicInferenceTests`          | Scheduler inference coverage for ergonomic APIs such as `QueryView`, direct resource methods, and generated job extensions. |
| `WorldRelationIndexTests`                       | Internal relation-index lifecycle coverage for relation type-id reuse and target release cleanup.                            |
| `WorldResourceStoreTests`                       | Internal resource-store lifecycle coverage for disposal and snapshot boxing round-trips.                                     |
| `QueryGeneratorTests`                           | Compiled query usage and filtering behavior from a consumer perspective.                                                    |
| `GeneratedQueryAndJobSourceGenIntegrationTests` | Source-generated query-spec and job-extension integration against runtime execution APIs.                                   |
| `GeneratedSystemMetadataResolverTests`          | Generated system metadata discovery, resolver behavior, and legacy direct-access read/write classification.                 |

## Quick Start

```bash
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj
```

## Design Notes

- Tests are consumer-first and validate ECS ergonomics as exposed by the public API.
- Runtime tests prioritize correctness for structural changes, deterministic playback, and fixed-capacity behavior.
- Snapshot restore coverage includes fail-closed defaults, explicit allow-list enforcement, malformed payload rejection, and no-mutation-on-rejection behavior.
- Source-generation integration tests ensure generated code stays aligned with runtime contracts.

## Release Gates

- `WorldApiContractTests` protects the intended public authoring surface, including exact non-error obsolete attributes and migration messages for legacy access, world-options, and command APIs. Member-level reflection covers obsolete members that the public type exporter does not track.
- `SystemContext` coverage protects the canonical command-stream constructor and property, exact null validation, scalar/world values, scheduler stream lifecycle, and both canonical-property and compatibility-wrapper identity with the original command stream.
- Legacy world-options coverage characterizes exact chunk-capacity mapping, ignored byte-size hints, retained `WorldConfig` defaults, and preserved null and parallelism validation behavior.
- Runtime and scheduler tests protect the three retained aliases as behavior-preserving forwarders, including mutable-write tracking and legacy access-metadata inference.
- `SystemManagerTests` and `SystemManagerErgonomicInferenceTests` protect scheduler conflict detection for both attribute-driven and inferred access metadata.
- `WorldAdvancedApiTests` protects snapshot fail-closed behavior, allow-list enforcement, and no-mutation-on-rejection semantics.
- `WorldRuntimeAllocationTests` protects hot-path allocation expectations after warmup.
- `GeneratedQueryAndJobSourceGenIntegrationTests` protects the generator-backed ergonomic API that application code is expected to use.

## Test Conventions

- Method naming follows `Method_WhenCondition_ShouldExpectation`.
- Runtime coverage is split into focused partial files under `Services/WorldRuntime*Tests.cs` for maintainability.
- `World` instances are created with `using var` to keep disposal behavior explicit and consistent.
- Assertions use FluentAssertions for readable failure messages and uniform style.
