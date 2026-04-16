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

## Design Notes

- Generators avoid reflection on hot paths by precomputing query and metadata structures.
- Generated code uses fully-qualified symbols for resilient compilation.
- `SystemMetadataGenerator` infers read/write sets from ECS iteration and direct access calls (`World.Query(...).ForEach(...)`, generated `Run(...)` / `RunParallel(...)` job extensions, `World.Run(...)`, `QueryCursor.ForEach(...)`, `QueryCursor.Run(...)`, `Read/Write/TryWrite`, and explicit resource APIs), in addition to `[Reads(typeof(...))]` / `[Writes(typeof(...))]` attributes.
- `ForEachJobSourceGenerator` keeps the ergonomic callsite as `Run(...)` / `RunParallel(...)` even for entity-aware jobs; the generated code routes those calls to the runtime `RunEntity(...)` / `RunParallelEntity(...)` members.
- `QuerySourceGenerator` reports `BECSG001` when unsupported ECS attributes are applied to a `[Query]` spec; unsupported attributes are ignored for generated query build code.
- Output is deterministic and incremental-safe.
