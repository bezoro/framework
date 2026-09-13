# Bezoro.ECS.SourceGen

Roslyn incremental source generators for `Bezoro.ECS` compile-time helpers.

`Bezoro.ECS.SourceGen` exists to support the `Bezoro.ECS` consumer surface. Application code in this solution is expected to depend on `Bezoro.ECS`; the generator project is infrastructure rather than a primary application-facing API.

## Types

| Type                        | Description                                                                                                                                         |
|-----------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| `QuerySourceGenerator`      | Generates `ICompiledQuerySpec` implementations from `[Query]` filter attributes (`[With]`/`[All]`, `[AnyOf]`/`[Any]`, `[Without]`/`[None]`, `[Optional]`, `[Changed]`, `[Added]`). |
| `ComponentCatalogGenerator` | Emits a compatibility stub; components are lazily registered at runtime.                                                                            |
| `SystemMetadataGenerator`   | Generates static system metadata catalog from discovered `ISystem` types.                                                                           |
| `ForEachJobSourceGenerator` | Generates `Run(job)` / `RunParallel(job)` extensions for `QueryView<TSpec>`, plus `Run(job)` extensions for `QueryCursor` and `World`, for accessible `IForEach<T...>` and `IForEachEntity<T...>` job structs (arity 1-4). |
| `SplitFieldSourceGenerator` | Generates split-group helper types and storage helpers for `[SplitFields]` components with `[SplitGroup]` field annotations.                        |

## Quick Start

Reference `Bezoro.ECS.SourceGen` as an analyzer through `Bezoro.ECS`, then declare a query specification:

```csharp
using Bezoro.ECS.Attributes;

[Query]
[With(typeof(Position))]
internal readonly partial struct MovingEntityQuery;
```

The generator completes the partial query type with an `ICompiledQuerySpec` implementation consumed by `World.Compile<MovingEntityQuery>()` and `World.Query<MovingEntityQuery>()`.

## API Reference

The project is analyzer infrastructure and does not expose an application runtime API. Its compile-time contract consists of generated implementations and diagnostics:

| Generator | Generated contract |
| --- | --- |
| `QuerySourceGenerator` | Query-spec metadata and `ICompiledQuerySpec` implementation. |
| `SystemMetadataGenerator` | Static read/write metadata for scheduler planning. |
| `ForEachJobSourceGenerator` | Typed `Run` and `RunParallel` extension methods for ECS jobs. |
| `SplitFieldSourceGenerator` | Split-field component helpers and storage metadata. |
| `ComponentCatalogGenerator` | Compatibility output for runtime component registration. |

## Feature Notes

- `BECSG001` reports unsupported ECS attributes on a query specification.
- Generated code is deterministic and uses fully qualified symbols.
- Consumer source remains the source of truth; generated files are build artifacts and must not be edited.

## Design Notes

- Generators avoid reflection on hot paths by precomputing query and metadata structures.
- Generated code uses fully-qualified symbols for resilient compilation.
- `SystemMetadataGenerator` infers read/write sets from ECS iteration and direct access calls (`World.Query(...).ForEach(...)`, generated `Run(...)` / `RunParallel(...)` job extensions, `World.Run(...)`, `QueryCursor.ForEach(...)`, `QueryCursor.Run(...)`, `Read/Write/TryWrite`, and explicit resource APIs), in addition to `[Reads(typeof(...))]` / `[Writes(typeof(...))]` attributes.
- `ForEachJobSourceGenerator` keeps the ergonomic callsite as `Run(...)` / `RunParallel(...)` even for entity-aware jobs; the generated code routes those calls to the runtime `RunEntity(...)` / `RunParallelEntity(...)` members.
- `QuerySourceGenerator` reports `BECSG001` when unsupported ECS attributes are applied to a `[Query]` spec; unsupported attributes are ignored for generated query build code.
- Output is deterministic and incremental-safe.
