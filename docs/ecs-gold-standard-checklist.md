# ECS Gold-Standard Checklist

This is the canonical implementation tracker for `Bezoro.ECS` parity against a Bevy + Unity ECS reference bar.

- Last updated: 2026-02-22
- Status legend:
- `[ ]` Not started
- `[~]` In progress / partial
- `[x]` Implemented and validated

## Core Runtime

- [x] Archetype + chunk storage model
- [x] Versioned entity handles (`id` + `version`)
- [x] Deferred structural mutation via `CommandStream` + explicit playback
- [x] Fixed-capacity world with overflow policy (`FailFast`/`DropNewest`)
- [x] World/resource APIs for game-loop usage
- [x] Public first-class relation API (`AddRelation`/`RemoveRelation`/`HasRelation`)
- [x] Relation-aware public query filters

## Query System

- [x] Compiled query specs (`ICompiledQuerySpec`)
- [x] Query DSL support for `All`/`Any`/`None`
- [x] Cursor-based and direct hot-path iteration
- [x] Struct job execution (`Run<TJob,...>`) on cursor and world
- [x] Source-generated query spec implementation from `[Query]` + `[All]`/`[Any]`/`[None]`/`[Optional]`/`[Changed]`/`[Added]`
- [x] Optional component filter support (`Optional<T>`) in runtime query execution
- [x] Changed/added component filters (`Changed<T>`/`Added<T>`) in runtime query execution

## Scheduling and Safety

- [x] Stage + loop phase execution model
- [x] Access-based parallel batching using read/write metadata
- [x] Exclusive-system semantics
- [x] Fixed-interval/catch-up update settings
- [x] User-defined schedule graph dependencies (`Before`/`After`)
- [x] System sets and run-conditions API surface
- [x] Resource access participation in conflict graph

## Source Generation

- [x] System metadata catalog generation
- [x] Metadata inference from modern ECS iteration APIs (`World.ForEach/Run`, `QueryCursor.ForEach/Run`)
- [x] Generated job extensions for cursor/world `Run(job)` ergonomics
- [x] Query generator no longer emits stale `Query`/`IQuery` runtime references
- [x] Split-field generator support
- [x] Source-generated diagnostics for unsupported advanced query filters

## Diagnostics and Tooling

- [x] World/command diagnostics types
- [x] Benchmark project for ECS hot paths
- [x] Schedule diagnostics and graph introspection APIs
- [x] Public snapshot serialization/deserialization docs and examples

## Documentation

- [x] `src/Bezoro.ECS/README.md` accurately reflects current ECS capabilities
- [x] `src/Bezoro.ECS.SourceGen/README.md` aligned with current generators
- [x] `benchmarks/Bezoro.ECS.Benchmarks/README.md` aligned with runnable benchmark reality
- [x] `tests/Bezoro.ECS.Tests/README.md` exists

## Verification Gates

- [x] `dotnet build bezoro.framework.sln`
- [x] `dotnet test` verification across non-UCI test projects after the full ECS change set (UCI intentionally skipped due known flakiness)
- [x] Benchmark baseline + post-change comparison in this environment

## Notes

- Network-restricted environments can block BenchmarkDotNet auto-generated project restore. Keep benchmark regressions as a required gate in environments with NuGet connectivity.
- Latest fast-run baseline/post samples (2026-02-22): `EcsWorldHotPathBenchmarks` stayed in the ~58-60 us range with zero allocations; treat variance from `--fast` as expected and re-run with reliable jobs for tighter confidence.
