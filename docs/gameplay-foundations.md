# Gameplay foundations

This expansion builds on the reviewed framework at `29b9f761`. It adds data-oriented building blocks for simulation, procedural generation, navigation, and persistence. The new runtime assemblies target .NET 10 and .NET Standard 2.1 and introduce no NuGet dependencies.

## Module boundaries

| Area | Contract |
| --- | --- |
| Core randomness | PCG32 state is a 16-byte readonly value. Seed initialization, range rejection, floating conversion, and shuffle order have a versioned algorithm identity. Weighted tables copy integer cumulative weights and sample without changing shared table state. |
| Core collections | Slot maps keep dense values and sparse generational identities in fixed storage. Ring buffers retain a fixed FIFO window. Removing or clearing entries releases reference payloads. Borrowed views and enumeration have explicit mutation limits. |
| Simulation timing | The clock advances immutable state from caller-supplied elapsed time. Scheduling uses integer simulation ticks, a bounded indexed heap, data payloads, and caller-provided drain buffers. |
| Simulation attributes | Finite definitions and ordered modifiers feed one calculator. Sets preallocate modifier slots and cache each attribute independently. Only modified attributes require reevaluation. |
| Navigation | Immutable grid snapshots can be shared. Each active search owns one reusable workspace and publishes a complete route to a supplied span. Costs are integer, with deterministic tie-breaking. |
| Persistence | The envelope checks bounded lengths, format/schema metadata, and CRC32 before exposing a payload. The file backend uses same-directory replacement and per-operation streams. Applications choose serialization and schema migration. |

Simulation references Core. Navigation and Persistence have no runtime project dependencies. None of these modules references ECS, Unity, or Godot. GameSystems remains the assembly for ECS-specific pipelines; applications can register standalone owners as world resources when useful.

## Memory and ownership

Capacities are explicit. Slot-map backing storage is approximately `capacity × (sizeof(T) + 16)` bytes plus array/object headers. A ring buffer holds one `T[capacity]`. Attribute modifiers use 16 bytes after removing eight bytes of x64 alignment padding; a layout regression first reproduced the previous 24-byte size. Navigation owns 32 bytes of workspace per cell, in addition to the immutable `ushort` grid and the caller's route buffer. Schedulers and attribute sets allocate their bounded storage at construction rather than retaining one task or closure per event.

Steady-state random draws, collection operations, clock advancement, scheduling, cached attribute reads, path queries, and envelope operations have allocation checks or measured workloads. This does not make initialization, user payload construction, file streams, or asynchronous I/O allocation-free. File reads use caller-owned destination memory and reject advertised payload lengths beyond the configured limit before reading payload data.

Slot, scheduler, and modifier handles identify live entries in one owner. Generation exhaustion retires a slot, and owner identity exhaustion rejects new owners. Handles are not persistent IDs. Save semantic identifiers and values, then construct new owners and handles on load.

## Threading and host integration

Readonly values and immutable maps/tables can be shared. Mutable collections, random state variables, schedulers, attribute sets, and search workspaces require one owner or external serialization. Separate owners can execute concurrently. Navigation rejects simultaneous use of one workspace. File-store operations own their streams and use filesystem commit ordering.

Unity and Godot scene APIs remain on the main thread. Independent simulation work can use the existing bounded ECS `WorldGroup` or an application worker pool. The foundations do not create hidden background simulation loops or a task per timer/path node. Persistence uses asynchronous I/O, while checksumming still consumes CPU on the execution context that calls it.

The [console example](../samples/Bezoro.Gameplay.ConsoleDemo/README.md) composes these contracts: separate random streams, a weighted route, a temporary attack modifier, 96 fixed steps, bounded history, and a schema-owned checkpoint. The [C# 9 consumer](../samples/Bezoro.UnityApiSmoke/README.md) checks API consumption against .NET Standard 2.1 and can compile against staged DLLs.

## Validation evidence

The integrated Release build completed with zero warnings and errors. The integrated run passed 2,747 tests across 12 projects, including Stockfish 19 integration on Linux. Subsequent packaging-documentation and modifier-layout regressions brought the validated inventory to 2,749 tests; the complete build-script and Simulation suites then passed 28 and 77 tests. No failures or skips occurred in these verification runs. UPM staging and the C# 9 consumer build against the staged .NET Standard DLLs passed. The public-type inventory was regenerated for the new APIs.

| Test project | Passed |
| --- | ---: |
| Build scripts | 28 |
| Core | 1,030 |
| Events | 88 |
| ECS | 287 |
| GameSystems | 102 |
| Logging | 30 |
| TypingSystem | 96 |
| Chess protocol | 701 |
| Chess facade | 195 |
| Simulation | 77 |
| Navigation | 36 |
| Persistence | 79 |

- PCG tests use the published 42/54 reference vector and pin floating conversions, shuffle order, snapshot continuation, and integer extremes, including a checked build.
- Collection tests compare seeded operation sequences with reference models and verify stale/foreign handles and released payloads.
- Scheduler tests include a reference queue; an independent review exercised 1,040,000 operations across 13 capacities and eight seeds.
- Attribute tests compare formulas and caches. An independent model checked 200,000 mutations and 600,000 comparisons, including finite extremes and 49,985 removed handles.
- Navigation tests compare 600 searches with an independent Dijkstra implementation, validate route steps and cost, and include a path costing `4,325,310,000`, above UInt32.
- Persistence tests cover corruption, limits, schema handling, deterministic cancellation, and atomic replacement. An independent CRC/bit-flip oracle checked 147,744 cases. A two-instance Linux stress performed 480 writes, 2,000 reads, and 400 deletes, with whole valid snapshots or NotFound and no temporary files retained.
- The publisher integration test redirects a fixture-only GitHub URL to a local bare repository. It verifies idempotence, removal of obsolete artifacts, retained history, and Conventional Commit messages without contacting GitHub.

Recorded measurements and their workload/source context are in the [attribute benchmarks](../benchmarks/Bezoro.Simulation.Benchmarks/README.md) and [pathfinding benchmarks](../benchmarks/Bezoro.Navigation.Benchmarks/README.md). Short runs on shared hardware are diagnostics; inspect the raw CSV error intervals and profile the target runtime before choosing frame budgets.

## Boundaries

The fixed-step clock uses exact 100 ns elapsed units. Converting each floating-point frame delta independently can change accumulated rounding; record exact elapsed values or tick inputs for replay. Preserved backlog and dropped time are visible in advancement results.

Attribute arithmetic uses ordered binary64 operations and finite saturation, followed by documented override and clamp rules. Modifier order is part of the result. A simulation requiring fixed-point arithmetic must select that numeric policy explicitly.

Navigation covers weighted rectangular grids. Large searches may exhaust a caller's expansion budget, and an incomplete query returns a status rather than a partial route. Cancellation is cooperative. Immutable grid changes require a new snapshot; borrowed query buffers must remain valid for the call.

Atomic file visibility requires a trusted local directory and a filesystem supporting replacement. Writes retain the previous save on failure before commit; cancellation after commit does not turn success into a reported failure. Flush requests are not a universal power-loss durability guarantee. Engine-specific WebGL, console, cloud, or virtual storage can implement the byte-oriented save-store interface.

Managed compilation and tests do not establish Unity Editor, IL2CPP, or Godot export compatibility. Validate the selected engine/player target and its stripping configuration. CI is reserved for consolidated checks and automatic main integration; development pushes do not run it.
