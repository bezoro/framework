# Changelog

## Unreleased

### API and behavior changes

- Runtime projects target .NET 10 and .NET Standard 2.1. Tests, samples, and benchmarks use .NET 10. The compiler language setting remains `latest`.
- The UPM package targets Unity 6 and still contains precompiled DLLs. It includes the required NuGet runtime dependency closure and upstream license notice.
- Manual chess gameplay is local. `UciGameEngineSession` accepts construction without an engine path; loading, moves, promotion, and undo do not wait for Stockfish. Automatic background analysis requires `EnableBackgroundAnalysis=true`.
- Missing evaluations remain absent rather than being represented as equal positions. Mate predictions no longer imply immediate checkmate.
- UCI node, node-rate, tablebase-hit, and elapsed-time counters and related constructor/deconstruction members use `ulong`.
- Grid constructors, coordinates, and disposed access reject invalid usage. Event buses reject registration after disposal. Code relying on permissive or incorrect behavior must handle these failures.

### Chess

- Strict structural FEN parsing without regular expressions or token-array allocation, with rank and counter validation.
- Legal king safety, en passant, castling, promotion, and repetition identity fixes; no king captures or phantom en passant.
- Engine-free protocol sessions, synchronous refresh, incremental repetition counts, and reuse of resulting-position legal moves.
- Terminal-result enforcement, clock freeze, fivefold repetition and 75-move automatic draws, timeout precedence, atomic setup validation, history-aware undo, and stale-analysis rejection.
- Dedicated `UciGameReviewAnalyzer`, immutable review results, rating-model and annotation policies, WDL, expected-points quality, finite budgets, cancellation, and progress. The documented accuracy formula is library-specific, not CAPS2.
- UCI search and transport lifecycle serialization, waiter/timer cleanup, cumulative counters, final primary-variation selection, and stop/go ordering.
- Context-aware analysis caching retains the base FEN and complete move history.
- Local facade construction allocates no engine runtime. State streams retain only the latest pending immutable snapshot and reject subscription races after disposal.

### Other modules

- Core grids, pool capacity and waiter lifecycle, duplicate returns, swapback collection boundaries, aliased enumeration, and null guards.
- Event-subscription identity and disposal synchronization.
- ECS native bounds and layout handling, restored-resource lifetime, isolated-world thread caches, failed creation/shutdown cleanup, command-stream ownership, and finite deltas.
- Typing completion, single enumeration of word sources, and removal of consumed words without losing unread content.
- Logging filters run before formatting.
- Timer input validation and precise long-running input timestamps.

### Validation and documentation

- Regression tests across every changed runtime module, reference perft coverage, Stockfish integration, and cross-platform build-script tests.
- Linux integration engine selection through `BEZORO_STOCKFISH_PATH` and Linux/Windows CI validation.
- Package dependency conflict detection, missing-artifact failures, protected staging paths, stable dependency metadata, and XML documentation.
- SemVer numeric/ordinal prerelease precedence, numeric-identifier validation, large version components, build metadata preservation, and preview bumps based on Conventional Commit feature/breaking intent.
- Updated module guides, root setup, contribution rules, architecture research, and review evidence. No release tag is created by this unreleased entry.
