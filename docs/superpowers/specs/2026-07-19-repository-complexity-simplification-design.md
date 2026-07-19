# Repository Complexity Simplification Design

## Status

Approved direction: aggressive public API sweep with compatibility-preserving deprecation.

This document defines the repository-wide design. Implementation is split into independently testable workstreams so that each change can be reviewed and benchmarked on its own merits.

## Goal

Minimize total cognitive and structural complexity across Bezoro.Framework while preserving correctness, maintainability, testability, efficiency, useful dormant capabilities, target-framework support, and migration paths for existing consumers.

The preferred result uses direct control flow, cohesive modules, explicit data shapes, shallow call chains, and the fewest abstractions that preserve real invariants or side effects.

## Current Evidence

The audited repository contains approximately 49,000 source lines and 32,000 test lines across nine source projects and eight test projects. The baseline full suite passes 1,777 tests.

Complexity is concentrated rather than uniform:

- ECS storage, direct iteration, and source-generation metadata inference.
- Chess/UCI session orchestration, local rules, clock adjudication, and asynchronous worker ownership.
- Core pool ownership, expression/reflection-based guards, broad convenience APIs, and repeated collection kernels.
- Logging payload construction and mutable ambient asynchronous context.
- Typing result construction and word-provider responsibilities.

The following complexity is supported by evidence and remains:

- Arity-specific ECS query kernels that avoid boxing, reflection, and allocation.
- Command-stream pooled buffers and dense/sparse playback paths covered by burst benchmarks.
- UCI transport, command, search, and output-dispatch boundaries that isolate I/O and concurrency.
- EventBus snapshot-and-dispatch behavior that protects reentrancy and concurrency.
- Explicit game-system transition methods whose names expose legal state changes.
- Potentially useful public or internal capabilities merely lacking current repository references.

## Compatibility Policy

The sweep is aggressive about establishing one canonical API, not about deleting compatibility.

1. Design every replacement first in a consumer-facing test.
2. Add the canonical member or type and verify the test fails for the expected reason.
3. Implement the smallest behavior satisfying the new contract.
4. Keep every superseded public member or type available with `[Obsolete("Use ... instead.")]`.
5. Make compatibility members forward directly to the canonical implementation whenever semantics match.
6. Migrate repository source, tests, benchmarks, samples, and documentation to canonical APIs so the repository itself builds without obsolete warnings.
7. Preserve public serialized property names and layouts unless an explicit round-trip compatibility test proves a safe migration.
8. Update XML documentation, project READMEs, samples, and public API baselines with every public change.
9. Do not remove a member or capability solely because no current repository reference exists.
10. Do not create a compatibility shim when no canonical replacement exists or when the proposed replacement does not reduce total complexity.

Deprecation is successful only when consumers gain an obviously smaller or safer surface. Mechanical renaming without a call-site benefit is out of scope.

Removing obsolete compatibility code is explicitly deferred to a later, separately authorized cleanup after downstream consumers have migrated. This goal establishes and validates the canonical framework surface while keeping that migration bridge intact.

## Workstream 1: Project Coupling and Quality Gates

Remove project and package references that provide no compile-time capability after verifying both target frameworks build without them. This applies to declarations, not reusable source code.

Candidate edges established by source inspection include:

- `Bezoro.Logging` to `Bezoro.Core`.
- `Bezoro.TypingSystem` to `Bezoro.Logging`.
- `Bezoro.GameSystems` to `Bezoro.Events`.
- `Bezoro.Chess.UCI` to `Bezoro.Logging`.
- `Microsoft.CSharp` package references in the chess projects when no dynamic binder is required.
- `Bezoro.Chess.UCI.Protocol` to `Bezoro.Core` if the single character-conversion dependency can be replaced by direct domain parsing.
- `Bezoro.Chess.UCI` to `Bezoro.Core` if its small set of generic convenience calls can be expressed directly without duplication.

Each edge is removed separately and accepted only after a dual-target build and affected tests. No source capability is deleted as part of this workstream.

Add a `Bezoro.Logging.Tests` project because logging currently contains ambient state, concurrency, formatting, and payload contracts without an isolated test boundary. The new project mirrors the source structure and uses xUnit plus FluentAssertions.

## Workstream 2: Core API and Ownership Simplification

### Object pool invariants

Unify synchronous and asynchronous acquisition around one internal acquisition kernel. Centralize slot reservation, creation rollback, discard accounting, statistics, and waiter signaling.

Fix these invariants:

- A reserved capacity slot is released if creation throws.
- Validation or reset rejection releases ownership and total capacity exactly once.
- Trimming, clearing, disposal, and policy rejection use one owned-discard path.
- A copied `PooledObjectHandle<T>` cannot return the same value twice.
- Foreign returns retain current compatibility behavior without corrupting owned-item accounting.

Keep the existing public handle type and make its copies share one internal reference-identity return state. A new public lease type is unnecessary unless the existing shape cannot satisfy exactly-once ownership without a breaking layout change.

### Guard APIs

Make the boolean `ThrowIf` overload canonical. Mark the expression-tree overload obsolete because expression compilation, structural-string caching, captured closure aliasing, and an unbounded global cache add complexity and can produce incorrect results.

The obsolete expression overload remains behaviorally correct during migration by evaluating its supplied predicate without sharing compiled captured state across expression instances. Remove the global expression cache and key types.

Mark reflection-based `ExceptionHelper` construction and the broad `ValidationHelper` facade obsolete in favor of direct standard exception construction and focused existing null/empty guards. Migrate internal callers. Compatibility methods remain and receive contract tests for their actual exception types, messages, and parameter names.

### Convenience surface

Make the general `Color(string)` and `Color(Color)` operations canonical. Mark named-color string wrappers and enum styling wrappers obsolete because they duplicate the general operation across hundreds of public methods. Retain general formatting primitives such as color, size, bold, and style composition.

Centralize `SwapbackArray<T>` lookup in one private index kernel used by `Contains`, `IndexOf`, `TryIndexOf`, and `TryRemove`. Add a non-nullable `TryIndexOf(T, out uint)` contract and mark the nullable-out overload obsolete, eliminating the representable `(false, non-null index)` state from the canonical API.

Replace public optional boolean traps only where two named operations are clearer and the existing default can remain compatible. Candidate operations include collection/pool clearing, singleton reset/disposal, and pooled-grid creation. Each replacement must reduce ambiguity at call sites; no options type is introduced for a two-state operation.

### Benchmarks

Separate `SwapbackArray` benchmark setup from measured operations. Keep explicitly named end-to-end churn cases, and add focused operation baselines where claims are made.

## Workstream 3: Typing API State Simplification

Make `TypingResult` state-specific factories public and canonical:

- `Match`
- `Mismatch`
- `Completed`
- `EmptyTarget`
- `PositionOutOfRange`

Mark the raw eight-parameter constructor obsolete. The compatibility constructor preserves layout and inputs, while canonical factories make contradictory status/flag/position combinations unavailable to normal callers. Derive `IsCorrect`, `IsComplete`, and `IsFaulted` from status where doing so preserves serialized and reflection-visible contracts.

Introduce a narrow atomic word-consumption contract centered on `TryGetNextWord`. The canonical interface owns only consumption. `ArrayWordProvider` retains collection mutation. File loading moves to a focused extension or adapter at the filesystem boundary. Mark the old check-then-get and file-I/O members obsolete and forward them during migration.

This new interface is justified because it removes exception-driven exhaustion, duplicate checks, and mixed I/O/mutable-collection responsibilities. It must not grow speculative asynchronous or randomization members.

## Workstream 4: ECS API, Storage, and Iteration Simplification

### Canonical public vocabulary

Make mutation explicit:

- `Read` and `TryRead` are canonical read operations.
- `Write` and `TryWrite` are canonical mutable-reference operations.
- `ReadResource`, `TryReadResource`, and `WriteResource` are canonical resource operations.
- Mark mutable `Get`, `GetResource`, and the duplicate `TryGetManaged` aliases obsolete.
- Correct source-generator metadata inference immediately so retained mutable aliases are classified as writes.

Mark `WorldOptions` and `World(WorldOptions)` obsolete in favor of `WorldConfig`. Preserve the adapter's current mapping and document that its byte-size setting never affected fixed-capacity world construction.

Make `CommandStream` the canonical deferred-mutation surface. Mark `CommandBuffer` and exact creation aliases obsolete when they add only vocabulary translation and no invariant, side-effect boundary, or test seam.

Make `QueryView<TSpec>` the canonical fluent query execution surface. Deprecate duplicate `World` iteration overload families only after proving each has an exact `QueryView` replacement. Retain low-level cursor and compiled-query APIs for allocation-sensitive consumers and source-generated integration.

### Internal orchestration

Route managed and unmanaged `QueryView` iteration through direct chunk traversal. Add an explicit read-only walker path so read traversal does not mark change versions. Remove entity materialization and repeated world location lookups from managed traversal.

For parallel entity-aware jobs, construct entity handles from each matched chunk's contiguous entity IDs and the stable version array. Remove the preliminary full-entity materialization pass and its scratch ownership.

Merge the identical direct and entity-aware executor interfaces and validation shells into one internal orchestration path. Retain arity-specific executor structs and inner loops.

Delegate accessor location and column resolution to `WorldEntityStore` after the `World` disposal guard. Retain the store capability and remove the duplicate state machine from the facade.

Centralize scalar set/overwrite and change-tracking invariants in one aggressively inlined internal kernel. Keep batch-specialized span/chunk-cache paths separate.

Do not remove dormant entity/storage/source-generator capability based only on non-use. Remove internal recognizers only when repository history, runtime contracts, and generator tests prove they target an obsolete API rather than a future extension point.

### Performance evidence

Run existing ECS benchmarks before and after material traversal changes. Add focused cases for mutable managed `QueryView`, parallel entity-aware execution, and any consolidated executor path not covered by current benchmarks. Accept no material regression beyond benchmark noise without an explicit clarity/performance tradeoff review.

## Workstream 5: Chess and UCI Simplification

### Local rules kernel

Create one internal parsed-position representation and one legality/attack/apply kernel shared by local FEN rules, tactical resolution, and move classification. Batch classification parses a position once and publishes one immutable result set.

Public extension methods remain façades over this kernel. The representation stays internal and compact; no public board abstraction is introduced.

Validate pins, castling, en passant, promotions, mate, stalemate, insufficient material, and representative perft-style cases. Compare throughput and allocations with `LocalRulesBenchmarks`.

### Clock and adjudication

Extract shared internal clock restoration, stage selection, elapsed-time, timeout, repetition, outcome, claimable-result, and fallback-move functions used by both playable session implementations.

Keep the two session classes separate because their orchestration and event models differ. Prefer a small immutable clock state plus pure functions over another manager object.

### Async ownership

Make position-analysis and classification workers generation-safe. A worker captures immutable generation and cancellation data, validates ownership before committing results, and retains worker identity until exit.

Move CPU classification to one explicit bounded background worker. Build a position's result dictionary locally and publish once instead of repeatedly locking individual results.

Remove the nested `Task.Run` used for each stderr line; the existing background stderr loop invokes its captured handler directly with the same exception isolation and ordering.

Centralize the repeated task-cancellation compatibility pattern. Use native `Task.WaitAsync` on `net9.0` and one internal polyfill on `netstandard2.1`.

### Public aliases and sample

Mark exact aliases obsolete when replacements already exist, including engine-move, human-move, best-move-event, and raw-line-event names identified by the audit. Do not deprecate an event when its proposed replacement has different semantics.

Unify interactive and redirected console command processing behind one command switch while keeping input acquisition and rendering mode-specific. Inline tiny routing predicates if they cease to represent an independent policy after consolidation.

## Workstream 6: Logging Simplification

Add behavior and concurrency coverage before refactoring.

Replace `AsyncLocal<Stack<string>>` with immutable linked scope nodes. Each scope captures and restores its previous node, so child execution contexts cannot mutate a shared stack.

Introduce one internal immutable log-input record that replaces the eleven positional parameters passed between private methods. This type is internal because it names an implementation data flow rather than a consumer contract.

Extract a pure internal formatter that transforms the input plus settings snapshot into a payload. Keep filtering, stage transition state, sequence generation, and event dispatch in `Logger` because they own side effects.

Preserve the existing public logger methods and `LogPayload` serialized shape during this pass. Public logging deprecation is not justified until the tested internal flow reveals a clearly smaller consumer API; aggressive sweep does not authorize speculative surface creation.

Correct README examples to match the real settings API. Add focused allocation benchmarks for no-context and nested-context emission before claiming allocation improvements.

## Testing Strategy

Every behavior or public API task follows red, green, refactor.

The implementation plans will specify exact tests, but the required evidence classes are:

- Contract tests for every canonical API and every obsolete forwarder.
- Regression tests for pool accounting, copied handles, captured predicates, worker generations, logger sibling contexts, and ECS write inference.
- Serialization/reflection shape tests where public records or property layouts are involved.
- Focused unit tests for shared pure kernels.
- Integration tests for transport, filesystem adapters, generated metadata, session facades, and source-generator output.
- Existing full-project tests after each workstream.
- Full solution test and build gates at completion.

Tests remain direct and behavior-focused. Architecture-only tests are added only for established repository contracts such as public API baselines, target support, and project dependency direction.

## Validation Gates

A workstream is complete only when all applicable gates pass:

1. New test fails for the intended reason before implementation.
2. New and affected tests pass after implementation.
3. Affected source projects build for every declared target framework.
4. Public API baselines are intentionally updated and checked.
5. XML documentation and project READMEs describe canonical and obsolete paths.
6. Changed samples compile and use canonical APIs.
7. Performance-sensitive changes have representative before/after BenchmarkDotNet evidence.
8. `git diff --check` reports no whitespace errors.
9. The full `dotnet test bezoro.framework.sln` passes.
10. `dotnet build bezoro.framework.sln` passes with zero warnings and errors.
11. `./scripts/Export-PublicApi.ps1 -Check` passes.

## Completion Rubric

The final score uses five equally weighted dimensions:

- Cognitive simplicity: direct flows, explicit names, bounded state, and shallow call chains.
- Structural simplicity: cohesive ownership, minimal delegation, necessary abstraction only, and reduced project coupling.
- API simplicity: one canonical path per operation, invalid states constrained, and complete migration guidance.
- Efficiency: algorithms, data layout, allocation, copies, and passes supported by representative evidence.
- Verification quality: behavior, compatibility, targets, documentation, and benchmarks proven by current checks.

A 10/10 requires no remaining high-confidence simplification whose benefit exceeds its compatibility, performance, or abstraction cost. It does not require deleting useful capability, eliminating justified low-level complexity, or forcing all code below an arbitrary line-count threshold.

## Delivery Boundaries

This design authorizes local source, test, benchmark, sample, project, API-baseline, and documentation edits within the repository. It does not authorize staging, committing, pushing, changing the existing pull request, publishing packages, or deploying artifacts.
