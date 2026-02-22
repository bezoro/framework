# Bezoro.ECS.SourceGen

Roslyn incremental source generators for `Bezoro.ECS` compile-time helpers.

## Types

| Type                        | Description                                                                                                                                         |
|-----------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| `QuerySourceGenerator`      | Generates `ICompiledQuerySpec` implementations from `[Query]` filter attributes (`[All]`, `[Any]`, `[None]`, `[Optional]`, `[Changed]`, `[Added]`). |
| `ComponentCatalogGenerator` | Emits a compatibility stub; components are lazily registered at runtime.                                                                            |
| `SystemMetadataGenerator`   | Generates static system metadata catalog from discovered `ISystem` types.                                                                           |
| `ForEachJobSourceGenerator` | Generates `Run(job)` extensions for `QueryCursor` and `World` for accessible `IForEach<T...>` job structs (arity 1-4), including nested types.      |
| `SplitFieldSourceGenerator` | Generates split-group helper types and storage helpers for `[SplitFields]` components with `[SplitGroup]` field annotations.                        |

## Design Notes

- Generators avoid reflection on hot paths by precomputing query and metadata structures.
- Generated code uses fully-qualified symbols for resilient compilation.
- `SystemMetadataGenerator` infers read/write sets from ECS iteration calls (`World.ForEach(...)`, `World.Run(...)`, `QueryCursor.ForEach(...)`, `QueryCursor.Run(...)`), in addition to `[Reads]`/`[Writes]` attributes.
- `QuerySourceGenerator` reports `BECSG001` when unsupported ECS attributes are applied to a `[Query]` spec; unsupported attributes are ignored for generated query build code.
- Output is deterministic and incremental-safe.
