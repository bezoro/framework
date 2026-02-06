# Bezoro.ECS.SourceGen

Roslyn incremental source generators for `Bezoro.ECS` compile-time helpers.

## Types

| Type | Description |
|---|---|
| `QuerySourceGenerator` | Generates query factory helpers from `[Query]` filters and bridges partial query structs to `IQuery`. |
| `ComponentCatalogGenerator` | Generates component registration catalog from discovered `IComponent` structs. |
| `SystemMetadataGenerator` | Generates static system metadata catalog from discovered `ISystem` types. |
| `ForEachJobSourceGenerator` | Generates `Query.ForEach(job)` extension overloads for accessible `IForEach<T...>` job structs (arity 1-4), including nested types. |
| `SplitFieldSourceGenerator` | Generates split-group helper types and storage helpers for `[SplitFields]` components with `[SplitGroup]` field annotations. |

## Design Notes

- Generators avoid reflection on hot paths by precomputing query and metadata structures.
- Generated code uses fully-qualified symbols for resilient compilation.
- `SystemMetadataGenerator` infers read/write sets from `Query.ForEach(...)` and `Query.ForEachRW(...)` calls inside `ISystem.Update(...)`, in addition to `[Reads]`/`[Writes]` attributes.
- Output is deterministic and incremental-safe.
