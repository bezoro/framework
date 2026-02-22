# Bezoro.ECS.Tests

Unit and integration tests for `Bezoro.ECS` runtime, scheduling, source generation integration, and API contracts.

## Types

| Type                                            | Description                                                                                                                  |
|-------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------|
| `WorldRuntimeTests`                             | End-to-end runtime behavior for entities, components, queries, command playback, overflow behavior, and lifecycle semantics. |
| `WorldApiContractTests`                         | Public API surface and contract-level behavior checks for `World`/`IWorld`.                                                  |
| `SystemManagerTests`                            | Scheduling behavior, access-conflict batching, and phase/stage execution semantics.                                          |
| `QueryGeneratorTests`                           | Compiled query usage and filtering behavior from a consumer perspective.                                                     |
| `GeneratedQueryAndJobSourceGenIntegrationTests` | Source-generated query-spec and job-extension integration against runtime execution APIs.                                    |
| `GeneratedSystemMetadataResolverTests`          | Generated system metadata discovery and resolver behavior.                                                                   |

## Quick Start

```bash
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj
```

## Design Notes

- Tests are consumer-first and validate ECS ergonomics as exposed by the public API.
- Runtime tests prioritize correctness for structural changes, deterministic playback, and fixed-capacity behavior.
- Source-generation integration tests ensure generated code stays aligned with runtime contracts.
