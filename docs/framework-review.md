# Framework review

Review date: 2026-10-29. Source baseline: `e86e66a8`. The review covered every runtime module, the ECS generator's integration, tests, packaging, and CI. Six agents divided the source areas; independent follow-up reviews checked the game reviewer and the changed collection/pool code.

## Findings and changes

| Area | Concrete failures addressed |
| --- | --- |
| Local chess | Malformed FEN ranks and counters, illegal king captures, phantom en passant, incorrect repetition identity, repeated board parsing and move generation |
| Match state | Play after resignation/draw, timeout precedence, inconsistent loaded state, lost repetition context, stale analysis, clocks running after termination |
| Game facade | Gameplay awaiting engines, promotion loss on failure, false checkmate from engine forecasts, streams depending on a blocked UI queue |
| UCI | Lifecycle races, stale bestmove crossing searches, leaked waiters/timers, summed cumulative counters, shallow/secondary scores overriding final primary evaluation, truncated 64-bit counters, concurrent Process.Exited registrations losing an exit waiter |
| Core | Invalid grid storage/coordinates, overflow, uninitialized pooled data, disposed access, aliasing and boundary errors in swapback collections, pool waiter/lifetime races, incorrect null guard |
| Events | Subscription handles colliding between buses and registrations surviving disposal races |
| ECS | Native destination/range errors, unsafe component strides, disposed storage access, duplicate resource disposal, runtime generic reflection, cross-thread type-cache interference, failed lifecycle cleanup |
| Typing | Completion state never becoming complete, repeated source enumeration, and unread words lost when removing consumed entries |
| Logging and game systems | Formatting discarded messages, invalid timer deltas, and input timestamps losing precision after long uptime |
| Delivery | Missing UPM dependency DLLs, silently omitted artifacts, conflicting assemblies, unsafe output directories, Windows-only integration assumptions, and incorrect SemVer precedence |

The review removed tests that explicitly asserted broken behavior and replaced them with contract-level regressions. Examples include capturing a king, accepting an out-of-range collection slot, and throwing a null guard for a non-null value. Large-capacity tests now exercise arithmetic without allocating several gigabytes.

## Architecture after the changes

Local play uses deterministic rules and game-owned state. The protocol match can be constructed without clients; the game facade can be constructed without an engine path. Engine search is an explicit analysis or opponent action. Review results are immutable and carry model identity. Rating calibration and special annotations are separate policies rather than implicit claims of Chess.com equivalence.

Hot-path changes reuse the resulting legal-move array, update repetition counts incrementally, avoid redundant full-history application, remove intermediate move strings and regular-expression parsing, and avoid formatting filtered messages. The local facade does not allocate external-engine clients, and its state stream retains only the most recent pending snapshot for slow readers. Stream subscription is synchronized with disposal. Native and pooled resources have explicit lifetime checks. The ECS keeps simple native layouts on its native path and uses typed managed storage for layouts that cannot safely share a marshaled stride.

## Validation

The complete Release solution compiled for .NET 10 and .NET Standard 2.1 with zero warnings and zero errors. The Release test suites passed all 2460 tests, with zero failures and zero skips, including a real Stockfish 19 process on Linux. The public-type inventory check, staging of the UPM dependency closure, unique Unity metadata GUIDs, documentation links, and C# 9 API-consumer compilation passed.

| Test project | Passed |
| --- | --- |
| Bezoro.Build.Tests | 26 |
| Bezoro.Chess.UCI.Protocol.Tests | 697 |
| Bezoro.Chess.UCI.Tests | 192 |
| Bezoro.Core.Tests | 942 |
| Bezoro.ECS.Tests | 287 |
| Bezoro.Events.Tests | 88 |
| Bezoro.GameSystems.Tests | 102 |
| Bezoro.Logging.Tests | 30 |
| Bezoro.TypingSystem.Tests | 96 |

A differential check generated 24 deterministic random games with seed `20261029`, up to 180 plies each. For all 4,082 resulting test positions, the library's sorted legal-move set and selected move's complete resulting FEN matched `python-chess` 1.11.2. FEN comparison included the en passant target after a double pawn push. This supplements the checked-in perft references and special-position regressions; it does not prove correctness for every possible position.

The NuGet direct/transitive dependency audit covered 25 projects and reported no known advisories on the review date. This is a dated package-database check, not a claim that dependencies can never contain vulnerabilities.

## Local-rule performance diagnostic

Release builds were compared with the initial commit on the same Linux x64 machine, Intel Xeon Platinum 8573C, using the .NET 10 runtime for both assemblies. Each workload received 10,000 warmup calls and seven samples of 50,000 calls. Times below are medians; managed allocations came from `GC.GetAllocatedBytesForCurrentThread`. The workload is the initial position and `e2e4`, not a claim about every position or total framework speed.

| Operation | Before | After | Allocations before | Allocations after |
| --- | --- | --- | --- | --- |
| `ApplyMove` | 3.987 µs | 0.658 µs | 4928 B | 920 B |
| `GetLegalMoves` | 3.190 µs | 1.565 µs | 3928 B | 1544 B |

The sample shows a reduction in both runtime and allocations. These are small diagnostic measurements on shared hardware, not Unity Mono/IL2CPP timings or a guarantee for other workloads. There are still allocations for immutable collections and UCI move strings. Use the checked-in `LocalGameplayBenchmarks` with BenchmarkDotNet for workload-specific profiling:

```sh
dotnet run -c Release --project benchmarks/Bezoro.Chess.UCI.Protocol.Benchmarks -- --filter '*LocalGameplayBenchmarks*'
```

A separate C# 9 consumer targeting .NET Standard 2.1 compiled successfully against the staged UPM DLLs, including local-match calls and review-option construction. This checks managed API consumption with Unity's documented language level; it is not a Unity Editor or player run.

## Explicit multithreading

The review analyzer supports a bounded pool of dedicated clients. It creates one async worker per active client, rather than a task per position. The default remains serial. Per-engine thread/hash requests and a configurable history limit make resource use explicit; engine hash is not the process's total RAM. Pure model and annotation transformations run sequentially after immutable position results have been assembled in chronological order.

`WorldGroup` steps independent data-only worlds with an explicit worker limit and joins workers before returning or reporting failure. Its default is degree one. The existing scheduler cooperates with nested work so a parallel world group does not create another set of workers for each world's inner jobs. Membership is immutable; worlds remain mutable owners with external-access restrictions. See the [ECS benchmark results](../benchmarks/Bezoro.ECS.Benchmarks/README.md) for raw measurements, including the tiny workload where sequential execution won.

Process stdout and stderr use asynchronous reads. Idle engines no longer retain workers in synchronous `ReadLine` calls. A shared .NET test process supplies exact bytes and lifecycle barriers across platforms, replacing shell/code-page-dependent fixtures. Process exit-wait registration/removal is serialized per process. Its regression reproduced lost handlers with sixteen concurrent waiters before the fix; twenty repeated focused runs then passed, covering 1280 concurrent registrations without increasing timeouts.

## Remaining boundaries

- Local rules cover orthodox chess. Chess960 and other variants are not implemented by the local rules even when an external engine supports them.
- Structural FEN validation intentionally permits authored board fragments. Game review performs additional engine-safety checks. Neither validator proves historical reachability of an arbitrary authored position.
- Insufficient-material detection is conservative. It does not solve every possible dead position involving blocked pawns or other immobility.
- Protocol match mutation and ECS world mutation require an owning thread or external serialization. Separate worlds can run on separate threads; this does not make simultaneous mutation of one world safe.
- The default clock source uses UTC. Use an injected advancing clock source for applications requiring immunity to system clock adjustments. Persistence timestamps and elapsed-time policy remain application decisions.
- ECS command playback can apply earlier commands before a later command fails. It guarantees cleanup, not transaction rollback. Restore and serialization paths still have reflection/metadata requirements described in the ECS README.
- .NET compilation and package staging do not establish Unity Editor, IL2CPP player, linker, WebGL, mobile, console, or Godot export compatibility. Those runtimes were not available for end-to-end validation here.
- Stockfish WDL is an engine self-play model. The default accuracy metric and annotation policies do not reproduce proprietary CAPS2 or Chess.com rating calibration. See [research and behavior](chess-review.md).

## Release handling

Work is split into Conventional Commits on `fix/framework-chess-robustness`. Breaking changes are marked in commits and summarized in [CHANGELOG.md](../CHANGELOG.md). Package versions remain derived from SemVer Git tags; development changes do not create a release tag or publish an unreviewed `upm` branch.
