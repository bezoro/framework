# Chess and UCI Complexity Simplification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Consolidate Chess/UCI rules and match-state logic, make asynchronous workers ownership-safe, establish canonical compatibility paths, and remove verified-unneeded chess dependencies without changing protocol behavior.

**Architecture:** `Bezoro.Chess.UCI.Protocol` owns the shared internal position, clock, adjudication, and cancellation kernels; `Bezoro.Chess.UCI` consumes them through the existing `InternalsVisibleTo` boundary. Public session and extension types remain façades, while CPU and engine workers use one captured generation, one worker at a time, and one atomic publication per work item.

**Tech Stack:** C# 13, .NET 9.0, .NET Standard 2.1, xUnit, FluentAssertions, NSubstitute, BenchmarkDotNet, immutable collections, and `System.Threading.Tasks`/`System.Threading.Channels`.

## Global Constraints

- Target `net9.0` and `netstandard2.1`; both targets must compile after every source task.
- Nullable warnings are errors, public APIs generate XML documentation, namespaces follow folders, and each new type has its own file.
- Every behavior or public API change follows red, green, refactor; design public compatibility behavior in consumer-facing tests first.
- Keep `UciPlayableMatchSession` and `UciGameEngineSession` separate; their orchestration and event models remain distinct.
- Keep the parsed board representation internal and compact; introduce no public board abstraction.
- Preserve UCI transport, command, search, and output-dispatch boundaries, protocol ordering, callback exception isolation, and useful dormant capabilities.
- Keep every superseded public member available and mark exact aliases `[Obsolete("Use ... instead.")]`; do not obsolete `InfoPvReceived` because `InfoReceived` has different semantics.
- Migrate repository source, tests, benchmarks, samples, and documentation to canonical APIs so the repository builds with zero warnings.
- Remove a project or package reference only after verifying the capability is absent or replaced without changing observable behavior.
- Do not stage, commit, push, publish, or deploy while executing this plan unless separately authorized; commit commands below are handoff checkpoints, not authorization.

## File Structure

### New production files

- `src/Bezoro.Chess.UCI.Protocol/Internal/LocalPositionState.cs`: the single parsed local chess-position data shape.
- `src/Bezoro.Chess.UCI.Protocol/Internal/LocalPositionRules.cs`: parsing, structural classification, legal moves, attack detection, application, tactical resolution, and batch classification.
- `src/Bezoro.Chess.UCI.Protocol/Internal/MatchClockCheckpoint.cs`: immutable internal clock history value.
- `src/Bezoro.Chess.UCI.Protocol/Internal/PlayableMatchClockRules.cs`: pure clock restoration, stage, elapsed-time, pause/resume, move-completion, timeout, and snapshot functions.
- `src/Bezoro.Chess.UCI.Protocol/Internal/PlayableMatchOutcome.cs`: internal terminal/claimable outcome value.
- `src/Bezoro.Chess.UCI.Protocol/Internal/PlayableMatchAdjudication.cs`: pure repetition, result, and fallback-move functions.
- `src/Bezoro.Chess.UCI.Protocol/Internal/PositionAnalysisWorkItem.cs`: immutable internal input captured by the position-analysis worker.
- `src/Bezoro.Chess.UCI.Protocol/Compatibility/TaskCompatibility.cs`: the only cross-target task cancellation wait implementation.

### New test files

- `tests/Bezoro.Chess.UCI.Protocol.Tests/Internal/LocalPositionRulesTests.cs`
- `tests/Bezoro.Chess.UCI.Protocol.Tests/Internal/PlayableMatchClockRulesTests.cs`
- `tests/Bezoro.Chess.UCI.Protocol.Tests/Internal/PlayableMatchAdjudicationTests.cs`
- `tests/Bezoro.Chess.UCI.Protocol.Tests/Compatibility/TaskCompatibilityTests.cs`
- `tests/Bezoro.Chess.UCI.Protocol.Tests/Internal/MoveClassificationCoordinatorTests.cs`
- `tests/Bezoro.Chess.UCI.Protocol.Tests/API/UciPositionAnalysisCoordinatorTests.cs`
- `tests/Bezoro.Chess.UCI.Protocol.Tests/Domain/Common/Helpers/BackgroundLoopManagerTests.cs`
- `tests/Bezoro.Chess.UCI.Protocol.Tests/API/PublicAliasDeprecationTests.cs`
- `tests/Bezoro.Chess.UCI.Protocol.Tests/ConsoleDemo/PlayableChessConsoleDemoTests.cs`

---

### Task 1: Remove only verified-unneeded chess dependencies

**Files:**
- Modify: `src/Bezoro.Chess.UCI.Protocol/API/Types/Fen.cs:1-251`
- Modify: `src/Bezoro.Chess.UCI.Protocol/GlobalUsings.cs:1-5`
- Modify: `src/Bezoro.Chess.UCI.Protocol/Bezoro.Chess.UCI.Protocol.csproj:12-25`
- Modify: `src/Bezoro.Chess.UCI/Bezoro.Chess.UCI.csproj:11-24`

**Interfaces:**
- Consumes: existing `Fen.Parse`, `Fen.Validate`, and `Fen.TryParseUciOutputLine` contracts.
- Produces: Protocol without a `Bezoro.Core` edge, UCI without a direct `Bezoro.Logging` edge, and both chess projects without `Microsoft.CSharp`.

- [ ] **Step 1: Record the dependency and dual-target baseline**

Run:

```powershell
dotnet build src/Bezoro.Chess.UCI.Protocol/Bezoro.Chess.UCI.Protocol.csproj -f net9.0
dotnet build src/Bezoro.Chess.UCI.Protocol/Bezoro.Chess.UCI.Protocol.csproj -f netstandard2.1
dotnet build src/Bezoro.Chess.UCI/Bezoro.Chess.UCI.csproj -f net9.0
dotnet build src/Bezoro.Chess.UCI/Bezoro.Chess.UCI.csproj -f netstandard2.1
rg -n "Bezoro\.Core|Bezoro\.Logging|Microsoft\.CSharp" src/Bezoro.Chess.UCI.Protocol src/Bezoro.Chess.UCI --glob '*.cs' --glob '*.csproj'
```

Expected: all four builds pass; the search shows Protocol Core usage only in `Fen.cs`/global using, Protocol Logging usage in transport helpers, UCI Core extension usage in public parsing/validation code, no UCI Logging source usage, and no dynamic binder source usage.

- [ ] **Step 2: Replace Protocol Core string guards with equivalent BCL checks**

Apply these exact substitutions in `Fen.cs`:

```csharp
if (string.IsNullOrWhiteSpace(line)) return false;

string trimmed = line.Trim();
if (trimmed.Length == 0) return false;
```

Use direct split tokens after `Validate` has succeeded:

```csharp
string piecePlacement = parts[0];
string castlingRights = parts.Length > 2 ? parts[2] : string.Empty;
string enPassantTarget = parts.Length > 3 ? parts[3] : string.Empty;
```

Use `string.IsNullOrEmpty(parts[index])` before integer parsing and active-color parsing, retaining the existing exception messages and `nameof(parts)` parameter name. Remove `using Bezoro.Core.Extensions;` from `Fen.cs` and `global using Bezoro.Core;` from Protocol global usings.

- [ ] **Step 3: Remove declarations whose capability is absent**

Delete the Protocol `Bezoro.Core` project reference, the UCI `Bezoro.Logging` project reference, and both `Microsoft.CSharp` package references. Retain Protocol's `Bezoro.Logging` reference because `ProcessUciTransport`, `ProcessHelper`, and `BackgroundLoopManager` call it. Retain UCI's `Bezoro.Core` reference because `ParsedMove` and `Promotion` currently expose Core validation exception behavior; changing those public exception contracts is not part of Workstream 5.

- [ ] **Step 4: Verify the affected parsing contract and both targets**

Run:

```powershell
dotnet test tests/Bezoro.Chess.UCI.Protocol.Tests/Bezoro.Chess.UCI.Protocol.Tests.csproj --filter "FullyQualifiedName~FenTests"
dotnet build src/Bezoro.Chess.UCI.Protocol/Bezoro.Chess.UCI.Protocol.csproj -f net9.0
dotnet build src/Bezoro.Chess.UCI.Protocol/Bezoro.Chess.UCI.Protocol.csproj -f netstandard2.1
dotnet build src/Bezoro.Chess.UCI/Bezoro.Chess.UCI.csproj -f net9.0
dotnet build src/Bezoro.Chess.UCI/Bezoro.Chess.UCI.csproj -f netstandard2.1
rg -n "Microsoft\.CSharp|ProjectReference Include=.*Bezoro\.Logging" src --glob 'Bezoro.Chess.UCI*.csproj'
```

Expected: tests and builds pass; no `Microsoft.CSharp` references remain; only Protocol retains the Logging project reference.

- [ ] **Step 5: Commit checkpoint**

```powershell
git add src/Bezoro.Chess.UCI.Protocol/API/Types/Fen.cs src/Bezoro.Chess.UCI.Protocol/GlobalUsings.cs src/Bezoro.Chess.UCI.Protocol/Bezoro.Chess.UCI.Protocol.csproj src/Bezoro.Chess.UCI/Bezoro.Chess.UCI.csproj
git commit -m "refactor(chess): remove unused project dependencies"
```

### Task 2: Consolidate local chess rules into one parsed-position kernel

**Files:**
- Create: `src/Bezoro.Chess.UCI.Protocol/Internal/LocalPositionState.cs`
- Create: `src/Bezoro.Chess.UCI.Protocol/Internal/LocalPositionRules.cs`
- Modify: `src/Bezoro.Chess.UCI.Protocol/Internal/LocalFenRules.cs:1-795`
- Delete: `src/Bezoro.Chess.UCI.Protocol/Internal/LocalMoveTacticsResolver.cs`
- Modify: `src/Bezoro.Chess.UCI.Protocol/API/Common/Extensions/FenMoveClassificationExtensions.cs:1-171`
- Modify: `tests/Bezoro.Chess.UCI.Protocol.Tests/API/Common/Extensions/FenMoveClassificationExtensionsTests.cs`
- Modify: `tests/Bezoro.Chess.UCI.Protocol.Tests/API/Common/Extensions/FenRulesExtensionsTests.cs`
- Create: `tests/Bezoro.Chess.UCI.Protocol.Tests/Internal/LocalPositionRulesTests.cs`
- Modify: `benchmarks/Bezoro.Chess.UCI.Protocol.Benchmarks/LocalRulesBenchmarks.cs:1-42`

**Interfaces:**
- Consumes: `Fen`, `MoveClassification`, and lowercase UCI notation.
- Produces: `LocalPositionRules.Parse(Fen)`, `GenerateLegalMoves(LocalPositionState)`, `ClassifyMove`, `ClassifyMovesFully`, `ApplyMove`, `IsKingInCheck`, `HasInsufficientMaterial`, and `BuildRepetitionKey`; public extension signatures do not change.

- [ ] **Step 1: Capture a before benchmark**

Run:

```powershell
dotnet run -c Release --project benchmarks/Bezoro.Chess.UCI.Protocol.Benchmarks/Bezoro.Chess.UCI.Protocol.Benchmarks.csproj -- --filter "*LocalRulesBenchmarks*" --exporters json
```

Expected: BenchmarkDotNet completes in Release and reports time and allocated bytes for opening/middlegame legal generation and full classification. Preserve the generated report outside source edits for the before/after comparison.

- [ ] **Step 2: Write failing consumer and kernel tests**

Add this perft-style and tactical matrix:

```csharp
[Theory]
[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 20)]
[InlineData("r3k2r/p1ppqpb1/bn2pnp1/2pP4/1p2P3/2N2N2/PPQBBPPP/R3K2R w KQkq - 0 1", 48)]
[InlineData("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 14)]
public void GenerateLegalMoves_WhenReferencePositionIsParsed_ShouldMatchDepthOneCount(string rawFen, int expected)
{
    var state = LocalPositionRules.Parse(Fen.Parse(rawFen)!.Value);
    LocalPositionRules.GenerateLegalMoves(state).Should().HaveCount(expected);
}

[Fact]
public void GenerateLegalMoves_WhenRookIsPinnedToKing_ShouldRejectSidewaysMove()
{
    var state = LocalPositionRules.Parse(Fen.Parse("4r2k/8/8/8/8/8/4R3/4K3 w - - 0 1")!.Value);
    LocalPositionRules.GenerateLegalMoves(state).Should().NotContain("e2d2");
}

[Theory]
[InlineData("7k/8/8/8/8/8/8/K7 w - - 0 1", true)]
[InlineData("7k/8/8/8/8/8/8/KB6 w - - 0 1", true)]
[InlineData("7k/8/8/8/8/8/8/KR6 w - - 0 1", false)]
public void HasInsufficientMaterial_WhenMaterialVaries_ShouldReturnExpected(string rawFen, bool expected)
{
    var state = LocalPositionRules.Parse(Fen.Parse(rawFen)!.Value);
    LocalPositionRules.HasInsufficientMaterial(state).Should().Be(expected);
}
```

Extend `FenMoveClassificationExtensionsTests` with a batch-equivalence test over castling, en passant, promotion, check, mate, and stalemate positions. For each legal move, assert `fen.ClassifyMovesFully(legalMoves)[move] == fen.ClassifyMoveFully(move)`.

- [ ] **Step 3: Run the tests to verify the kernel is absent**

Run:

```powershell
dotnet test tests/Bezoro.Chess.UCI.Protocol.Tests/Bezoro.Chess.UCI.Protocol.Tests.csproj --filter "FullyQualifiedName~LocalPositionRulesTests|FullyQualifiedName~FenMoveClassificationExtensionsTests"
```

Expected: FAIL to compile with `CS0103` because `LocalPositionRules` does not exist.

- [ ] **Step 4: Add the single parsed-position type**

Create:

```csharp
namespace Bezoro.Chess.UCI.Protocol.Internal;

internal readonly record struct LocalPositionState(
    char[] Board,
    char ActiveColor,
    string CastlingRights,
    string EnPassantTarget,
    int HalfmoveClock,
    int FullmoveNumber);
```

The board index is always `rank * 8 + file`, with `a1 == 0` and `h8 == 63`. No dictionary or square-string key is stored.

- [ ] **Step 5: Move all rule primitives into `LocalPositionRules`**

Create one static class containing the direction arrays once and these exact entry points:

```csharp
internal static LocalPositionState Parse(Fen fen);
internal static ImmutableArray<string> GenerateLegalMoves(LocalPositionState state);
internal static MoveClassification ClassifyMove(LocalPositionState state, string move);
internal static ImmutableDictionary<string, MoveClassification> ClassifyMovesFully(
    LocalPositionState state,
    IEnumerable<string> legalMoves,
    CancellationToken ct = default);
internal static MoveClassification ClassifyMoveFully(
    LocalPositionState state,
    string move);
internal static LocalPositionState ApplyMove(
    LocalPositionState state,
    string move,
    MoveClassification structural);
internal static Fen ToFen(LocalPositionState state);
internal static bool IsKingInCheck(LocalPositionState state, char color);
internal static bool HasInsufficientMaterial(LocalPositionState state);
internal static string BuildRepetitionKey(Fen fen);
```

`ClassifyMove` must read `state.Board` by parsed source/destination index; en passant reads the captured pawn index directly. `ClassifyMoveFully` applies onto a cloned 64-square board and calls the same `IsKingInCheck` and `HasAnyLegalMove` functions used by legal generation. `ClassifyMovesFully` normalizes and validates each move, checks cancellation once per move, and fills one ordinal immutable-dictionary builder without reparsing the FEN.

Move, without semantic edits, the current pawn/knight/sliding/king generation, castling, attack, castling-right update, promotion, square-formatting, and FEN-formatting bodies from `LocalFenRules` and `LocalMoveTacticsResolver` into this class. While moving `HasInsufficientMaterial`, enumerate with an integer index rather than `Array.IndexOf`:

```csharp
for (var square = 0; square < state.Board.Length; square++)
{
    char piece = state.Board[square];
    if (piece == '\0' || char.ToLowerInvariant(piece) == 'k')
        continue;

    switch (char.ToLowerInvariant(piece))
    {
        case 'b': bishops.Add((piece, square)); break;
        case 'n': knights.Add(piece); break;
        default: majorsOrPawns.Add(piece); break;
    }
}
```

- [ ] **Step 6: Reduce existing classes to façades and remove the duplicate resolver**

`LocalFenRules` must contain only argument validation and delegation. Its public-to-internal flow is:

```csharp
var state = LocalPositionRules.Parse(fen);
var legalMoves = LocalPositionRules.GenerateLegalMoves(state);
if (!legalMoves.Contains(normalizedMove, StringComparer.Ordinal))
    throw new InvalidOperationException("The move is not legal in the supplied FEN position.");

var structural = LocalPositionRules.ClassifyMove(state, normalizedMove);
return LocalPositionRules.ToFen(LocalPositionRules.ApplyMove(state, normalizedMove, structural));
```

Update `FenMoveClassificationExtensions` so single-move methods parse once and batch methods parse once before their loops. Delete `ExpandBoard`, `GetPieceAt`, duplicated structural helpers, and `LocalMoveTacticsResolver.cs` after no references remain.

- [ ] **Step 7: Separate measured legal generation from measured classification**

Cache legal moves in benchmark fields:

```csharp
private readonly ImmutableArray<string> _openingLegalMoves = OpeningFen.GetLegalMoves();
private readonly ImmutableArray<string> _middlegameLegalMoves = MiddlegameFen.GetLegalMoves();

[Benchmark(Description = "Fully classify cached opening legal moves")]
public int ClassifyMovesFully_Opening() =>
    OpeningFen.ClassifyMovesFully(_openingLegalMoves).Count;

[Benchmark(Description = "Fully classify cached middlegame legal moves")]
public int ClassifyMovesFully_Middlegame() =>
    MiddlegameFen.ClassifyMovesFully(_middlegameLegalMoves).Count;
```

Keep the two existing legal-generation benchmarks as separate operations.

- [ ] **Step 8: Run green tests, both targets, and the after benchmark**

Run:

```powershell
dotnet test tests/Bezoro.Chess.UCI.Protocol.Tests/Bezoro.Chess.UCI.Protocol.Tests.csproj --filter "FullyQualifiedName~LocalPositionRulesTests|FullyQualifiedName~FenRulesExtensionsTests|FullyQualifiedName~FenMoveClassificationExtensionsTests"
dotnet build src/Bezoro.Chess.UCI.Protocol/Bezoro.Chess.UCI.Protocol.csproj -f net9.0
dotnet build src/Bezoro.Chess.UCI.Protocol/Bezoro.Chess.UCI.Protocol.csproj -f netstandard2.1
dotnet run -c Release --project benchmarks/Bezoro.Chess.UCI.Protocol.Benchmarks/Bezoro.Chess.UCI.Protocol.Benchmarks.csproj -- --filter "*LocalRulesBenchmarks*" --exporters json
```

Expected: all tests/builds pass; all reference counts match; fully classified batch results equal single-move results; cached batch classification allocates less than the recorded implementation. If runtime moves beyond normal BenchmarkDotNet noise, retain the consolidated kernel but record the measured clarity/performance tradeoff before continuing.

- [ ] **Step 9: Commit checkpoint**

```powershell
git add src/Bezoro.Chess.UCI.Protocol/Internal src/Bezoro.Chess.UCI.Protocol/API/Common/Extensions/FenMoveClassificationExtensions.cs tests/Bezoro.Chess.UCI.Protocol.Tests/API/Common/Extensions tests/Bezoro.Chess.UCI.Protocol.Tests/Internal benchmarks/Bezoro.Chess.UCI.Protocol.Benchmarks/LocalRulesBenchmarks.cs
git commit -m "refactor(chess): consolidate local rules kernel"
```

### Task 3: Extract immutable clock state and pure clock rules

**Files:**
- Create: `src/Bezoro.Chess.UCI.Protocol/Internal/MatchClockCheckpoint.cs`
- Create: `src/Bezoro.Chess.UCI.Protocol/Internal/PlayableMatchClockRules.cs`
- Create: `tests/Bezoro.Chess.UCI.Protocol.Tests/Internal/PlayableMatchClockRulesTests.cs`
- Modify: `src/Bezoro.Chess.UCI.Protocol/API/UciPlayableMatchSession.cs:17-35,632-662,947-1153,1344-1406,1465-1475`
- Modify: `src/Bezoro.Chess.UCI/API/UciGameEngineSession.cs:1260-1295,1664-1870,1933-1986,2282-2292`

**Interfaces:**
- Consumes: `PlayableMatchTimeControl`, `PlayableMatchClockSetup`, `PlayableMatchClockRestore`, explicit timestamps, and side-to-move characters.
- Produces: immutable `MatchClockCheckpoint` values and pure `PlayableMatchClockRules` functions shared by both sessions.

- [ ] **Step 1: Write failing pure clock tests**

Cover initialization, delay, increment, staged added time, pause/resume, exact restore, invalid active side/stage, and timeout clamping. The central progression test is:

```csharp
[Fact]
public void CompleteMove_WhenDelayAndIncrementApply_ShouldChargeOnlyMainElapsedAndAdvanceSide()
{
    var start = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
    var control = new PlayableMatchTimeControl(
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(3));
    var checkpoint = PlayableMatchClockRules.Initialize(control, 'w', start)!.Value;

    var next = PlayableMatchClockRules.CompleteMove(
        control,
        checkpoint,
        'w',
        start + TimeSpan.FromSeconds(8));

    next.WhiteRemaining.Should().Be(TimeSpan.FromSeconds(27));
    next.BlackRemaining.Should().Be(TimeSpan.FromSeconds(30));
    next.WhiteMovesCompleted.Should().Be(1);
    next.ActiveColor.Should().Be('b');
}
```

- [ ] **Step 2: Run the clock tests to verify the API is absent**

Run:

```powershell
dotnet test tests/Bezoro.Chess.UCI.Protocol.Tests/Bezoro.Chess.UCI.Protocol.Tests.csproj --filter "FullyQualifiedName~PlayableMatchClockRulesTests"
```

Expected: FAIL to compile with `CS0103` for `PlayableMatchClockRules`.

- [ ] **Step 3: Add the immutable checkpoint and clock functions**

Create the checkpoint with the exact existing fields:

```csharp
internal readonly record struct MatchClockCheckpoint(
    TimeSpan WhiteRemaining,
    TimeSpan BlackRemaining,
    char ActiveColor,
    int WhiteMovesCompleted,
    int BlackMovesCompleted,
    int ActiveStageIndex,
    DateTimeOffset TurnStartedAtUtc,
    DateTimeOffset? PausedAtUtc,
    TimeSpan PausedAccumulated);
```

Create `PlayableMatchClockRules` with these pure methods:

```csharp
internal static PlayableMatchClockRestore? CreateRestore(
    Fen currentFen,
    PlayableMatchClockSetup? setup,
    PlayableMatchTimeControl? control,
    DateTimeOffset now);
internal static MatchClockCheckpoint? Initialize(
    PlayableMatchTimeControl? control,
    char activeColor,
    DateTimeOffset now);
internal static MatchClockCheckpoint Restore(
    PlayableMatchTimeControl? control,
    char activeColor,
    PlayableMatchClockRestore restore);
internal static MatchClockCheckpoint CompleteMove(
    PlayableMatchTimeControl control,
    MatchClockCheckpoint checkpoint,
    char movingSide,
    DateTimeOffset now);
internal static MatchClockCheckpoint Pause(MatchClockCheckpoint checkpoint, DateTimeOffset now) =>
    checkpoint.PausedAtUtc.HasValue ? checkpoint : checkpoint with { PausedAtUtc = now };
internal static MatchClockCheckpoint Resume(MatchClockCheckpoint checkpoint, DateTimeOffset now) =>
    !checkpoint.PausedAtUtc.HasValue
        ? checkpoint
        : checkpoint with
        {
            PausedAccumulated = checkpoint.PausedAccumulated + (now - checkpoint.PausedAtUtc.Value),
            PausedAtUtc = null
        };
internal static PlayableMatchClockState? Snapshot(
    PlayableMatchTimeControl? control,
    MatchClockCheckpoint? checkpoint,
    DateTimeOffset now);
internal static void EnsureTurnHasTimeRemaining(
    PlayableMatchTimeControl? control,
    PlayableMatchClockState? clock);
```

Move the current `ResolveCompletedMoveCounts`, `ComputeElapsed`, stage-index, stage-settings, added-stage-time, clamp, restore validation, and move-completion arithmetic unchanged into these methods. Derive pause state from `PausedAtUtc.HasValue`; remove the separate `_isClockPaused` field from both sessions.

- [ ] **Step 4: Replace both session clock implementations with thin calls**

Both sessions retain `List<MatchClockCheckpoint> _clockHistory`. Initialization and restore become:

```csharp
var restore = PlayableMatchClockRules.CreateRestore(currentFen, setup.Clock, _timeControl, now);
_clockHistory.Clear();
var checkpoint = restore.HasValue
    ? PlayableMatchClockRules.Restore(_timeControl, currentFen.ActiveColor, restore.Value)
    : PlayableMatchClockRules.Initialize(_timeControl, currentFen.ActiveColor, now);
if (checkpoint.HasValue)
    _clockHistory.Add(checkpoint.Value);
```

Pause/resume replace only the last checkpoint with `Pause`/`Resume`; completion appends `CompleteMove`; snapshots call `Snapshot`. Pass `_utcNowProvider()` in Protocol and `DateTimeOffset.UtcNow` in UCI so public timing semantics remain unchanged.

- [ ] **Step 5: Run pure and façade clock tests**

Run:

```powershell
dotnet test tests/Bezoro.Chess.UCI.Protocol.Tests/Bezoro.Chess.UCI.Protocol.Tests.csproj --filter "FullyQualifiedName~PlayableMatchClockRulesTests|FullyQualifiedName~UciPlayableMatchSessionProtocolTests|FullyQualifiedName~UciPlayableMatchSessionEventContractTests"
dotnet test tests/Bezoro.Chess.UCI.Tests/Bezoro.Chess.UCI.Tests.csproj --filter "FullyQualifiedName~UciGameEngineSessionClockRestoreTests|FullyQualifiedName~UciGameEngineSessionLoadMatchTests|FullyQualifiedName~PauseAndResumeClockAsync"
dotnet build src/Bezoro.Chess.UCI.Protocol/Bezoro.Chess.UCI.Protocol.csproj -f netstandard2.1
dotnet build src/Bezoro.Chess.UCI/Bezoro.Chess.UCI.csproj -f netstandard2.1
```

Expected: pure tests and both session contract suites pass; exact restore, stage index, delay, timeout, pause, and resume values remain unchanged.

- [ ] **Step 6: Commit checkpoint**

```powershell
git add src/Bezoro.Chess.UCI.Protocol/Internal/MatchClockCheckpoint.cs src/Bezoro.Chess.UCI.Protocol/Internal/PlayableMatchClockRules.cs src/Bezoro.Chess.UCI.Protocol/API/UciPlayableMatchSession.cs src/Bezoro.Chess.UCI/API/UciGameEngineSession.cs tests/Bezoro.Chess.UCI.Protocol.Tests/Internal/PlayableMatchClockRulesTests.cs
git commit -m "refactor(chess): share immutable clock rules"
```

### Task 4: Extract shared adjudication, repetition, and fallback selection

**Files:**
- Create: `src/Bezoro.Chess.UCI.Protocol/Internal/PlayableMatchOutcome.cs`
- Create: `src/Bezoro.Chess.UCI.Protocol/Internal/PlayableMatchAdjudication.cs`
- Create: `tests/Bezoro.Chess.UCI.Protocol.Tests/Internal/PlayableMatchAdjudicationTests.cs`
- Modify: `src/Bezoro.Chess.UCI.Protocol/API/UciPlayableMatchSession.cs:1155-1215,1408-1434,1477-1485`
- Modify: `src/Bezoro.Chess.UCI/API/UciGameEngineSession.cs:1631-1662,1872-1931,2294-2302`

**Interfaces:**
- Consumes: position, legal-move count/list, clock snapshot, time control, forced result, claim policy, and a sequence of historical FEN values.
- Produces: one shared terminal/claimable outcome and one deterministic fallback priority: mate, check, capture, promotion, first legal move.

- [ ] **Step 1: Write failing outcome and fallback tests**

Add theories for forced result precedence, timeout, checkmate, stalemate, fifty-move automatic/claim-required, insufficient material, and threefold repetition. Add:

```csharp
[Fact]
public void TrySelectFallbackMove_WhenMateExists_ShouldPreferMateOverEarlierCapture()
{
    var fen = Fen.Parse("7k/5Q2/7K/8/8/8/8/8 w - - 0 1")!.Value;
    var legalMoves = fen.GetLegalMoves();

    PlayableMatchAdjudication.TrySelectFallbackMove(fen, legalMoves, out var move).Should().BeTrue();
    fen.ClassifyMoveFully(move).IsMate.Should().BeTrue();
}
```

- [ ] **Step 2: Verify the new API is absent**

Run the `PlayableMatchAdjudicationTests` filter. Expected: compile failure `CS0103`.

- [ ] **Step 3: Implement the pure outcome value and functions**

Create:

```csharp
internal readonly record struct PlayableMatchOutcome(
    PlayableMatchResult Result,
    PlayableMatchResult? ClaimableResult);
```

Implement these exact functions:

```csharp
internal static PlayableMatchOutcome Evaluate(
    Fen fen,
    int legalMoveCount,
    PlayableMatchClockState? clock,
    PlayableMatchTimeControl? control,
    PlayableMatchResult forcedResult,
    PlayableMatchClaimableDrawPolicy claimPolicy,
    int repetitionCount);

internal static int CountRepetitions(Fen currentFen, IEnumerable<Fen> positions)
{
    string key = LocalPositionRules.BuildRepetitionKey(currentFen);
    return positions.Count(position =>
        LocalPositionRules.BuildRepetitionKey(position) == key);
}

internal static bool TrySelectFallbackMove(
    Fen fen,
    IReadOnlyList<string> legalMoves,
    out string move);
```

`Evaluate` uses the current precedence: forced result, automatic timeout, no-legal-move mate/stalemate, fifty-move, insufficient material, repetition, none. Claim-required results populate only `ClaimableResult`. `TrySelectFallbackMove` calls `fen.ClassifyMovesFully(legalMoves)` once and selects by the existing priority.

- [ ] **Step 4: Replace both private adjudication blocks**

Each session supplies its history as FEN values and calls the shared functions. Protocol parses `_baseFen` plus valid `PlayedMove.PositionKey`; UCI supplies `_state.BaseFen` plus `GameMoveEvent.ResultingFen`. Remove both private `MatchOutcome` types and duplicate outcome/fallback methods.

- [ ] **Step 5: Run pure and façade result tests**

Run:

```powershell
dotnet test tests/Bezoro.Chess.UCI.Protocol.Tests/Bezoro.Chess.UCI.Protocol.Tests.csproj --filter "FullyQualifiedName~PlayableMatchAdjudicationTests|FullyQualifiedName~UciPlayableMatchSessionProtocolTests|FullyQualifiedName~UciPlayableMatchSessionEventContractTests"
dotnet test tests/Bezoro.Chess.UCI.Tests/Bezoro.Chess.UCI.Tests.csproj --filter "FullyQualifiedName~UciGameEngineSessionGameEventModelTests"
```

Expected: all result, draw, timeout, repetition, and fallback tests pass with unchanged event ordering.

- [ ] **Step 6: Commit checkpoint**

```powershell
git add src/Bezoro.Chess.UCI.Protocol/Internal/PlayableMatchOutcome.cs src/Bezoro.Chess.UCI.Protocol/Internal/PlayableMatchAdjudication.cs src/Bezoro.Chess.UCI.Protocol/API/UciPlayableMatchSession.cs src/Bezoro.Chess.UCI/API/UciGameEngineSession.cs tests/Bezoro.Chess.UCI.Protocol.Tests/Internal/PlayableMatchAdjudicationTests.cs
git commit -m "refactor(chess): share match adjudication rules"
```

### Task 5: Centralize cross-target task cancellation waits

**Files:**
- Create: `src/Bezoro.Chess.UCI.Protocol/Compatibility/TaskCompatibility.cs`
- Create: `tests/Bezoro.Chess.UCI.Protocol.Tests/Compatibility/TaskCompatibilityTests.cs`
- Modify: `src/Bezoro.Chess.UCI.Protocol/Domain/Common/Helpers/GateManager.cs:18-155`
- Modify: `src/Bezoro.Chess.UCI.Protocol/Internal/MoveClassificationCoordinator.cs:59-157`
- Modify: `src/Bezoro.Chess.UCI/API/UciGameEngineSession.cs:879-907`

**Interfaces:**
- Produces: `TaskCompatibility.WaitAsync(Task, CancellationToken)` and `TaskCompatibility.WaitAsync<T>(Task<T>, CancellationToken)`.

- [ ] **Step 1: Write failing cancellation-race tests**

Test completed, faulted, pre-cancelled, mid-wait cancelled, and successful generic tasks. The fault contract is:

```csharp
[Fact]
public async Task WaitAsync_WhenSourceFaultsBeforeCancellation_ShouldPropagateSourceException()
{
    using var cts = new CancellationTokenSource();
    var source = Task.FromException<int>(new InvalidOperationException("boom"));

    await FluentActions.Awaiting(() => TaskCompatibility.WaitAsync(source, cts.Token))
        .Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
}
```

- [ ] **Step 2: Verify compile failure**

Run the `TaskCompatibilityTests` filter. Expected: `CS0103` because the helper does not exist.

- [ ] **Step 3: Implement both target paths once**

```csharp
namespace Bezoro.Chess.UCI.Protocol.Compatibility;

internal static class TaskCompatibility
{
    public static async Task WaitAsync(Task task, CancellationToken ct)
    {
#if NET9_0
        await task.WaitAsync(ct).ConfigureAwait(false);
#else
        await WaitCoreAsync(task, ct).ConfigureAwait(false);
#endif
    }

    public static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken ct)
    {
#if NET9_0
        return await task.WaitAsync(ct).ConfigureAwait(false);
#else
        await WaitCoreAsync(task, ct).ConfigureAwait(false);
        return await task.ConfigureAwait(false);
#endif
    }

#if !NET9_0
    private static async Task WaitCoreAsync(Task task, CancellationToken ct)
    {
        if (!ct.CanBeCanceled || task.IsCompleted)
        {
            await task.ConfigureAwait(false);
            return;
        }

        ct.ThrowIfCancellationRequested();
        var cancellation = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = ct.Register(
            static state => ((TaskCompletionSource<object?>)state!).TrySetResult(null),
            cancellation);

        if (task != await Task.WhenAny(task, cancellation.Task).ConfigureAwait(false))
            throw new OperationCanceledException(ct);

        await task.ConfigureAwait(false);
    }
#endif
}
```

- [ ] **Step 4: Replace all three duplicated waits**

Use `TaskCompatibility.WaitAsync(...)` in `GateManager`, `MoveClassificationCoordinator.WaitAsync`, and `UciGameEngineSession.WaitForClassificationAsync`; delete their local cancellation-TCS implementations.

- [ ] **Step 5: Verify both target implementations**

Run the helper tests plus Protocol and UCI builds for both target frameworks. Expected: all pass, and `rg -n "WaitWithCancellation|cancelTcs|cancellationTask"` finds no duplicated compatibility implementation in the chess projects.

- [ ] **Step 6: Commit checkpoint**

```powershell
git add src/Bezoro.Chess.UCI.Protocol/Compatibility/TaskCompatibility.cs src/Bezoro.Chess.UCI.Protocol/Domain/Common/Helpers/GateManager.cs src/Bezoro.Chess.UCI.Protocol/Internal/MoveClassificationCoordinator.cs src/Bezoro.Chess.UCI/API/UciGameEngineSession.cs tests/Bezoro.Chess.UCI.Protocol.Tests/Compatibility/TaskCompatibilityTests.cs
git commit -m "refactor(chess): centralize task cancellation waits"
```

### Task 6: Make move classification one bounded generation-owned worker

**Files:**
- Modify: `src/Bezoro.Chess.UCI.Protocol/Internal/MoveClassificationCoordinator.cs:10-246`
- Modify: `src/Bezoro.Chess.UCI.Protocol/Internal/LocalPositionRules.cs`
- Create: `tests/Bezoro.Chess.UCI.Protocol.Tests/Internal/MoveClassificationCoordinatorTests.cs`

**Interfaces:**
- Consumes: `LocalPositionRules.ClassifyMovesFully(..., CancellationToken)` from Task 2 and `TaskCompatibility` from Task 5.
- Produces: asynchronous enqueue, single worker concurrency, captured generations, and one immutable dictionary publication per position.

- [ ] **Step 1: Write failing worker-ownership tests**

Use an internal constructor accepting:

```csharp
Func<Fen, ImmutableArray<string>, CancellationToken,
    ImmutableDictionary<string, MoveClassification>> classifyFully
```

Write one test whose classifier blocks on `ManualResetEventSlim`: `Enqueue` must return before the classifier is released. Write a second test that blocks generation 1, calls `Cancel`, enqueues the same position for generation 2, releases generation 1, and asserts only generation 2 publishes. Track an `Interlocked` active counter and assert maximum concurrency is one.

- [ ] **Step 2: Verify expected failure**

Run the new test class. Expected: compile failure because the internal constructor is absent; after adding only the constructor against current execution, the enqueue test fails because classification runs inline.

- [ ] **Step 3: Replace lifetime swapping with worker-owned state**

Use these fields:

```csharp
private readonly Func<Fen, ImmutableArray<string>, CancellationToken,
    ImmutableDictionary<string, MoveClassification>> _classifyFully;
private long _generation;
private long _workerGeneration;
private CancellationTokenSource? _workerCts;
private Task? _workerTask;
```

The default delegate parses once through `LocalPositionRules`; the test constructor supplies its delegate. `StartWorkerIfNeededLocked` captures the current generation and a new CTS, then uses `Task.Run(() => RunWorker(generation, cts.Token))`. Never assign `RunWorker` directly while holding `_sync`.

- [ ] **Step 4: Build locally and publish atomically**

For each dequeued position, read its existing structural snapshot once, run `_classifyFully` outside the lock, merge previously resolved entries once, then under the lock commit only when `generation == _generation` and cancellation has not been requested:

```csharp
if (generation == _generation && !ct.IsCancellationRequested)
{
    _classificationsByPosition[position.PositionKey] = completed;
    _queuedPositionKeys.Remove(position.PositionKey);
    if (_waiters.Remove(position.PositionKey, out var waiter))
        waiter.TrySetResult(completed);
}
```

In `finally`, clear `_workerTask`/`_workerCts` only when `_workerGeneration == generation`, dispose that worker's CTS, and start the current generation if pending work exists. `CancelPendingAndRetain` increments `_generation`, clears/cancels waiters and requested state, and cancels the captured worker CTS outside the lock without nulling worker identity.

- [ ] **Step 5: Run worker, classification, and session tests**

Run:

```powershell
dotnet test tests/Bezoro.Chess.UCI.Protocol.Tests/Bezoro.Chess.UCI.Protocol.Tests.csproj --filter "FullyQualifiedName~MoveClassificationCoordinatorTests|FullyQualifiedName~FenMoveClassificationExtensionsTests|FullyQualifiedName~UciPlayableMatchSession"
```

Expected: enqueue is non-blocking, old generations cannot commit, maximum classifier concurrency is one, cancellation completes prior waiters as cancelled, and session classification contracts pass.

- [ ] **Step 6: Commit checkpoint**

```powershell
git add src/Bezoro.Chess.UCI.Protocol/Internal/MoveClassificationCoordinator.cs src/Bezoro.Chess.UCI.Protocol/Internal/LocalPositionRules.cs tests/Bezoro.Chess.UCI.Protocol.Tests/Internal/MoveClassificationCoordinatorTests.cs
git commit -m "fix(chess): make classification worker generation safe"
```

### Task 7: Make position analysis generation-safe without expanding the public API

**Files:**
- Modify: `src/Bezoro.Chess.UCI.Protocol/API/UciPositionAnalysisCoordinator.cs:10-262`
- Create: `src/Bezoro.Chess.UCI.Protocol/Internal/PositionAnalysisWorkItem.cs`
- Create: `tests/Bezoro.Chess.UCI.Protocol.Tests/API/UciPositionAnalysisCoordinatorTests.cs`
- Retain: `tests/Bezoro.Chess.UCI.Protocol.Tests/API/UciPositionAnalysisCoordinatorIntegrationTests.cs`

**Interfaces:**
- Consumes: a dedicated `UciEngineClient` or an internal work-item delegate.
- Produces: one engine worker at a time; cancelled generations cannot repopulate cleared cache state; public `Cancel()` remains synchronous and source-compatible.

- [ ] **Step 1: Write a failing stale-commit test with a deterministic seam**

Add this internal work item in its own file and an internal constructor accepting `Func<PositionAnalysisWorkItem, CancellationToken, Task<PositionAnalysisResult>>`:

```csharp
internal readonly record struct PositionAnalysisWorkItem(
    string PositionKey,
    ImmutableArray<string> Moves,
    char SideToMove,
    char PlayerColor,
    ImmutableArray<string> LegalMoves);
```

In the test, generation 1 ignores cancellation until released, then returns result A; call `Cancel`, enqueue the same key for generation 2 returning result B, release A, and assert `GetAnalysisAsync` returns B, `TryGetAnalysis` caches B, and maximum delegate concurrency is one.

- [ ] **Step 2: Verify expected failure**

Run the non-integration test class. Expected: compile failure because the work-item constructor is absent; after adding only the seam, the current implementation fails by committing result A or overlapping workers.

- [ ] **Step 3: Bind each worker to immutable generation and cancellation data**

Use the same worker-owned fields and lifecycle as Task 6. Replace the `_client` field with a readonly `_analyzeAsync` delegate. The public constructor validates its `client` argument and captures that local client in this production delegate; the internal constructor assigns the supplied delegate:

```csharp
async (request, ct) =>
{
    await _client.SetPositionAsync(Fen.Default, request.Moves, ct).ConfigureAwait(false);
    return await _client.AnalyzePositionAsync(
        request.SideToMove,
        request.PlayerColor,
        request.LegalMoves,
        _multiPvMoveTimeMs,
        _fallbackMoveTimeMs,
        ct).ConfigureAwait(false);
}
```

Before committing success or failure, require `generation == _generation`, the token not cancelled, and the position key still tracked. `Cancel` and `CancelPendingAndRetainCompleted` increment the generation, clear the appropriate dictionaries/queues, cancel captured waiters, and request cancellation outside the lock; they do not set `_workerTask = null`. The old worker's `finally` starts queued current-generation work only after the old engine call exits.

- [ ] **Step 4: Verify unit and Stockfish integration behavior**

Run:

```powershell
dotnet test tests/Bezoro.Chess.UCI.Protocol.Tests/Bezoro.Chess.UCI.Protocol.Tests.csproj --filter "FullyQualifiedName~UciPositionAnalysisCoordinatorTests"
dotnet test tests/Bezoro.Chess.UCI.Protocol.Tests/Bezoro.Chess.UCI.Protocol.Tests.csproj --filter "FullyQualifiedName~UciPositionAnalysisCoordinatorIntegrationTests"
```

Expected: deterministic generation tests pass; FIFO/cache Stockfish integration tests remain green; no new `CancelAsync` or public runner abstraction exists.

- [ ] **Step 5: Commit checkpoint**

```powershell
git add src/Bezoro.Chess.UCI.Protocol/API/UciPositionAnalysisCoordinator.cs src/Bezoro.Chess.UCI.Protocol/Internal/PositionAnalysisWorkItem.cs tests/Bezoro.Chess.UCI.Protocol.Tests/API/UciPositionAnalysisCoordinatorTests.cs
git commit -m "fix(chess): make position analysis generation safe"
```

### Task 8: Remove the per-stderr-line nested thread-pool hop

**Files:**
- Modify: `src/Bezoro.Chess.UCI.Protocol/Domain/Common/Helpers/BackgroundLoopManager.cs:237-257,402-427,454-464`
- Create: `tests/Bezoro.Chess.UCI.Protocol.Tests/Domain/Common/Helpers/BackgroundLoopManagerTests.cs`
- Retain: `tests/Bezoro.Chess.UCI.Protocol.Tests/Domain/ProcessUciTransportEventTests.cs`

**Interfaces:**
- Produces: ordered, serial stderr callbacks on the already-background stderr loop with the same swallowed-handler-exception contract.

- [ ] **Step 1: Add characterization coverage**

Construct a `BackgroundLoopManager`, feed `"one\ntwo\nthree\n"` through a `MemoryStream`/`StreamReader`, make the handler record each line and throw on `"two"`, start and await the stderr loop, then assert the recorded order is exactly `one`, `two`, `three` and no error was reported.

- [ ] **Step 2: Run characterization before editing**

Run the new test plus `ProcessUciTransportEventTests` stderr filters. Expected: PASS; this refactor intentionally changes allocation/scheduling, not observable behavior.

- [ ] **Step 3: Make the already-background loop synchronous**

Replace the nested helper with:

```csharp
private void InvokeStderrHandler(string line)
{
    try
    {
        stderrReceived?.Invoke(line);
    }
    catch
    {
        // External handler exceptions must not terminate the stderr loop.
    }
}

private void RunStderrLoop(StreamReader stderr)
{
    while (true)
    {
        string? line = TryReadLine(stderr);
        if (line is null) return;
        if (line.Length > 0) InvokeStderrHandler(line);
    }
}
```

Start it with the same outer error boundary, without an async state machine:

```csharp
_stderrLoopTask = Task.Run(
    () =>
    {
        try
        {
            RunStderrLoop(stderr);
        }
        catch (Exception ex)
        {
            reportError(ex, "Stderr loop faulted.");
        }
    },
    CancellationToken.None);
```

Delete `InvokeStderrHandlerAsync` and the async stderr-loop state machine only; do not alter stdout/write-loop behavior.

- [ ] **Step 4: Rerun characterization and transport tests**

Expected: ordered callback, handler exception isolation, redirected/disabled stderr integration, and both target builds pass.

- [ ] **Step 5: Commit checkpoint**

```powershell
git add src/Bezoro.Chess.UCI.Protocol/Domain/Common/Helpers/BackgroundLoopManager.cs tests/Bezoro.Chess.UCI.Protocol.Tests/Domain/Common/Helpers/BackgroundLoopManagerTests.cs
git commit -m "refactor(chess): remove nested stderr task hop"
```

### Task 9: Deprecate exact public aliases and migrate repository callers

**Files:**
- Modify: `src/Bezoro.Chess.UCI.Protocol/API/UciPlayableMatchSession.cs:348-357,500-509`
- Modify: `src/Bezoro.Chess.UCI.Protocol/API/UciEngineClient.cs:56-64,82-89`
- Create: `tests/Bezoro.Chess.UCI.Protocol.Tests/API/PublicAliasDeprecationTests.cs`
- Modify: `tests/Bezoro.Chess.UCI.Protocol.Tests/API/UciPlayableMatchSessionIntegrationTests.cs`
- Modify: `tests/Bezoro.Chess.UCI.Protocol.Tests/Domain/UciEngineClientOutputDispatchTests.cs`
- Modify: `samples/Bezoro.Chess.UCI.Protocol.ConsoleDemo/PlayableChessConsoleDemo.cs:326,338`
- Modify: `src/Bezoro.Chess.UCI.Protocol/README.md`

**Interfaces:**
- Produces: canonical `PlayControlledMoveAsync`, `ApplyMove`, `BestMoveMessageReceived`, and `RawLineReceived`; obsolete aliases remain callable.

- [ ] **Step 1: Write failing reflection and forwarding tests**

Assert exact obsolete messages:

```csharp
[Theory]
[InlineData(typeof(UciPlayableMatchSession), "PlayEngineMoveAsync", "Use PlayControlledMoveAsync instead.")]
[InlineData(typeof(UciPlayableMatchSession), "ApplyHumanMove", "Use ApplyMove instead.")]
[InlineData(typeof(UciEngineClient), "BestMoveReceived", "Use BestMoveMessageReceived instead.")]
[InlineData(typeof(UciEngineClient), "LineReceived", "Use RawLineReceived instead.")]
public void CompatibilityMember_WhenInspected_ShouldDeclareCanonicalReplacement(
    Type type, string memberName, string message)
{
    var member = type.GetMember(memberName).Single();
    member.GetCustomAttribute<ObsoleteAttribute>()!.Message.Should().Be(message);
}
```

Under a narrowly scoped `#pragma warning disable CS0618`, retain one behavior test for each alias: method aliases produce the same move/application behavior as their canonical target; event aliases receive the same best-move/raw-line payload as canonical events.

- [ ] **Step 2: Verify red assertions**

Run `PublicAliasDeprecationTests`. Expected: FAIL because the four attributes are absent.

- [ ] **Step 3: Add exact deprecations without removing members**

Add:

```csharp
[Obsolete("Use PlayControlledMoveAsync instead.")]
public Task<EngineMoveResult> PlayEngineMoveAsync(CancellationToken ct = default) =>
    PlayControlledMoveAsync(ct);

[Obsolete("Use ApplyMove instead.")]
public void ApplyHumanMove(string move) => ApplyMove(move);

[Obsolete("Use BestMoveMessageReceived instead.")]
public event Action<string, string>? BestMoveReceived;

[Obsolete("Use RawLineReceived instead.")]
public event Action<string>? LineReceived;
```

Keep the two compatibility events' current separately guarded publication so an exception in an obsolete subscriber cannot suppress canonical subscribers. Do not obsolete `InfoPvReceived`.

- [ ] **Step 4: Migrate all repository use to canonical members**

Replace integration-test and sample calls with `ApplyMove`/`PlayControlledMoveAsync`. Replace non-compatibility event tests with typed `BestMoveMessageReceived` and `RawLineReceived`. Keep obsolete names only in the dedicated compatibility tests, source declarations, and migration documentation.

- [ ] **Step 5: Update XML docs and README migration guidance**

Document each obsolete member beside its canonical replacement, state that `BestMoveReceived` projects `UciBestMoveMessage` into two strings, and keep `InfoPvReceived` listed as supported rather than deprecated because it is a PV-specific convenience event.

- [ ] **Step 6: Verify warnings and public shape**

Run alias tests, both chess test projects, both-target builds, `rg` for obsolete names, and `& .\scripts\Export-PublicApi.ps1 -Check`. Expected: behavior passes, no obsolete warnings occur outside the suppressed compatibility tests, and the public-type baseline is unchanged.

- [ ] **Step 7: Commit checkpoint**

```powershell
git add src/Bezoro.Chess.UCI.Protocol/API/UciPlayableMatchSession.cs src/Bezoro.Chess.UCI.Protocol/API/UciEngineClient.cs src/Bezoro.Chess.UCI.Protocol/README.md tests/Bezoro.Chess.UCI.Protocol.Tests samples/Bezoro.Chess.UCI.Protocol.ConsoleDemo/PlayableChessConsoleDemo.cs
git commit -m "refactor(chess): deprecate exact compatibility aliases"
```

### Task 10: Unify console command processing behind one switch

**Files:**
- Modify: `samples/Bezoro.Chess.UCI.Protocol.ConsoleDemo/PlayableChessConsoleDemo.cs:343-475`
- Delete: `samples/Bezoro.Chess.UCI.Protocol.ConsoleDemo/PlayableTurnCommandRouter.cs`
- Delete: `tests/Bezoro.Chess.UCI.Protocol.Tests/ConsoleDemo/PlayableTurnCommandRouterTests.cs`
- Create: `tests/Bezoro.Chess.UCI.Protocol.Tests/ConsoleDemo/PlayableChessConsoleDemoTests.cs`

**Interfaces:**
- Produces: `PlayableChessConsoleDemo.ReadTurnCommandAsync(...)`, an internal shared command loop; input acquisition and rendering remain mode-specific.

- [ ] **Step 1: Write a failing shared-flow test**

Create three internal `UciEngineClient` instances over substitute transports, construct a manual session and a `PlayableMatchState` for `Fen.Default` with its legal moves, then pass a queue-backed reader returning `"bad"` followed by `"e2e4"`. Assert the shared method returns a normalized `Move` command and invoked the reader twice. Add a second test returning `"undo"` and assert no move validation occurs.

- [ ] **Step 2: Verify expected failure**

Run `PlayableChessConsoleDemoTests`. Expected: compile failure because `ReadTurnCommandAsync` does not exist.

- [ ] **Step 3: Introduce the single command switch**

Add:

```csharp
internal static async Task<PlayableMatchCommand> ReadTurnCommandAsync(
    UciPlayableMatchSession session,
    PlayableMatchState state,
    Func<Task<string>> readInputAsync,
    Action? beforeRead = null)
{
    while (true)
    {
        beforeRead?.Invoke();
        var command = PlayableMatchCommandParser.Parse(NormalizeInput(await readInputAsync()));
        switch (command.Kind)
        {
            case PlayableMatchCommandKind.Quit:
            case PlayableMatchCommandKind.LoadFen:
            case PlayableMatchCommandKind.Undo:
                return command;
            case PlayableMatchCommandKind.Moves:
                await PrintLegalMovesAsync(session);
                continue;
            case PlayableMatchCommandKind.History:
                PrintMoveHistory(session);
                continue;
            case PlayableMatchCommandKind.Invalid:
                Console.WriteLine(command.Error);
                continue;
            case PlayableMatchCommandKind.Move when state.LegalMoves.ContainsUciMove(command.Move!):
                return command;
            case PlayableMatchCommandKind.Move:
                Console.WriteLine("That move is not legal in the current position.");
                await PrintLegalMovesAsync(session);
                continue;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command.Kind, "Unknown playable command kind.");
        }
    }
}
```

Interactive mode supplies the existing `ReadInteractiveInputAsync` delegate and no `beforeRead`; redirected mode supplies `Task.FromResult(ReadRequiredLine())` and a `beforeRead` action that prints the board and prompt. Delete both duplicated loops, the two-predicate router, and its tests.

- [ ] **Step 4: Verify sample behavior and compilation**

Run console tests, parser tests, and `dotnet build samples/Bezoro.Chess.UCI.Protocol.ConsoleDemo/Bezoro.Chess.UCI.Protocol.ConsoleDemo.csproj`. Expected: shared-flow tests pass, the parser remains unchanged, and the sample uses only canonical session methods.

- [ ] **Step 5: Commit checkpoint**

```powershell
git add samples/Bezoro.Chess.UCI.Protocol.ConsoleDemo tests/Bezoro.Chess.UCI.Protocol.Tests/ConsoleDemo
git commit -m "refactor(chess): unify console command routing"
```

### Task 11: Update documentation and run the complete Workstream 5 gates

**Files:**
- Modify: `src/Bezoro.Chess.UCI.Protocol/README.md`
- Modify: `src/Bezoro.Chess.UCI/README.md`
- Modify: `tests/Bezoro.Chess.UCI.Protocol.Tests/README.md`
- Modify: `tests/Bezoro.Chess.UCI.Tests/README.md`
- Verify: `benchmarks/Bezoro.Chess.UCI.Protocol.Benchmarks/LocalRulesBenchmarks.cs`

**Interfaces:**
- Produces: current canonical API/migration documentation and complete correctness/performance evidence.

- [ ] **Step 1: Update project documentation**

Protocol README must describe the one parsed local-rules kernel, parse-once batch classification, immutable shared clock/adjudication functions, generation-owned analysis workers, and all four obsolete aliases with exact replacements. UCI README must state that clock and adjudication are Protocol-owned internals consumed by the game façade. Test READMEs must list the new pure-kernel, generation-race, cancellation-compatibility, alias-forwarding, and stderr-order coverage.

- [ ] **Step 2: Confirm no stale source or compatibility references remain**

Run:

```powershell
rg -n "LocalMoveTacticsResolver|PlayableTurnCommandRouter|WaitWithCancellation|cancelTcs|InvokeStderrHandlerAsync" src tests benchmarks samples
rg -n "\b(PlayEngineMoveAsync|ApplyHumanMove|BestMoveReceived|LineReceived)\b" src tests benchmarks samples --glob '*.cs'
```

Expected: the first command returns no matches; the second returns only obsolete declarations, guarded publication, and dedicated compatibility tests.

- [ ] **Step 3: Run affected tests and dual-target builds**

```powershell
dotnet test tests/Bezoro.Chess.UCI.Protocol.Tests/Bezoro.Chess.UCI.Protocol.Tests.csproj
dotnet test tests/Bezoro.Chess.UCI.Tests/Bezoro.Chess.UCI.Tests.csproj
dotnet build src/Bezoro.Chess.UCI.Protocol/Bezoro.Chess.UCI.Protocol.csproj -f net9.0
dotnet build src/Bezoro.Chess.UCI.Protocol/Bezoro.Chess.UCI.Protocol.csproj -f netstandard2.1
dotnet build src/Bezoro.Chess.UCI/Bezoro.Chess.UCI.csproj -f net9.0
dotnet build src/Bezoro.Chess.UCI/Bezoro.Chess.UCI.csproj -f netstandard2.1
```

Expected: every command succeeds with zero warnings and zero errors.

- [ ] **Step 4: Run the representative release benchmark and compare evidence**

```powershell
dotnet run -c Release --project benchmarks/Bezoro.Chess.UCI.Protocol.Benchmarks/Bezoro.Chess.UCI.Protocol.Benchmarks.csproj -- --filter "*LocalRulesBenchmarks*" --exporters json
```

Expected: legal-generation performance remains within normal BenchmarkDotNet noise; cached batch classification shows reduced allocations and no material throughput regression relative to Task 2's before report.

- [ ] **Step 5: Run repository-wide completion gates**

```powershell
dotnet test bezoro.framework.sln
dotnet build bezoro.framework.sln
& .\scripts\Export-PublicApi.ps1 -Check
git diff --check
```

Expected: the full suite passes, the solution builds with zero warnings/errors, public type baselines are current, and the diff has no whitespace errors.

- [ ] **Step 6: Review the final diff for ownership and compatibility**

Run:

```powershell
git diff --stat
git diff -- src/Bezoro.Chess.UCI.Protocol src/Bezoro.Chess.UCI tests/Bezoro.Chess.UCI.Protocol.Tests tests/Bezoro.Chess.UCI.Tests benchmarks/Bezoro.Chess.UCI.Protocol.Benchmarks samples/Bezoro.Chess.UCI.Protocol.ConsoleDemo
```

Confirm that no dormant capability was deleted for lack of references, public serialized types were not reshaped, session/event ordering remains covered, and every cancelled worker checks generation before publication.

- [ ] **Step 7: Commit checkpoint**

```powershell
git add src/Bezoro.Chess.UCI.Protocol/README.md src/Bezoro.Chess.UCI/README.md tests/Bezoro.Chess.UCI.Protocol.Tests/README.md tests/Bezoro.Chess.UCI.Tests/README.md
git commit -m "docs(chess): document simplified UCI architecture"
```

## Execution Notes

- Execute tasks in order. Tasks 6 and 7 depend on Tasks 2 and 5; Tasks 3 and 4 intentionally separate clock arithmetic from game adjudication.
- Use a fresh implementation worktree through `superpowers:using-git-worktrees` when execution begins.
- The listed commit commands are review boundaries only. The approved design explicitly does not authorize staging or committing.
- Do not broaden this plan into UCI parser, transport state-machine, engine-command, search-coordinator, or output-dispatch redesign; those boundaries were audited as justified.
