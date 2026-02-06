# Bezoro.ECS.SourceGen

Roslyn incremental source generators for `Bezoro.ECS` compile-time helpers.

## Types

| Type | Description |
|---|---|
| `QuerySourceGenerator` | Generates query factory helpers from `[Query]` filters and bridges partial query structs to `IQuery`. |
| `ComponentCatalogGenerator` | Generates component registration catalog from discovered `IComponent` structs. |
| `SystemMetadataGenerator` | Generates static system metadata catalog from discovered `ISystem` types. |
| `ForEachJobSourceGenerator` | Generates `Query.ForEach(job)` extension overloads for `IForEach<T1,T2>` job structs. |

## Design Notes

- Generators avoid reflection on hot paths by precomputing query and metadata structures.
- Generated code uses fully-qualified symbols for resilient compilation.
- Output is deterministic and incremental-safe.
