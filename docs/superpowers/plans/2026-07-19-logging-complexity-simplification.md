# Logging Complexity Simplification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Isolate Logging behavior with tests, eliminate mutable ambient stack sharing, and turn payload construction into a pure internal transformation without changing the public logger or payload contract.

**Architecture:** `Logger` remains the owner of filtering, stage transitions, sequence allocation, clock/thread capture, and event dispatch. Public calls create one immutable `LogInput`; immutable linked async-context nodes provide branch-safe ambient state; a captured `LogSettingsSnapshot` and `LogEventContext` feed a pure `LogPayloadFormatter`. A dedicated test project and allocation benchmark project protect behavior and cost.

**Tech Stack:** C# 13, .NET 9, .NET Standard 2.1, xUnit, FluentAssertions, BenchmarkDotNet, PowerShell.

## Global Constraints

- Add behavior and concurrency coverage before refactoring each path.
- Keep all public `Logger` methods, conditional attributes, optional parameters, caller attributes, `LoggerSettings` members, enums/config types, and every `LogPayload` property unchanged.
- Preserve filtering, category muting, exception details, message formatting, stage-divider behavior, grouping, file/caller metadata, styles, async hierarchy order, event ordering, and release call-site elision.
- Keep `net9.0` and `netstandard2.1` building with zero warnings; use only APIs available on both targets or a focused compatibility implementation.
- Keep immutable worker-produced data and event dispatch on the caller's current execution context; do not add a transport, queue, sink abstraction, or public deprecation.
- Keep one top-level type per file and namespaces aligned with folders.
- Run BenchmarkDotNet only in Release. Do not claim an allocation improvement without before/after reports for the same benchmark names.
- Do not stage or commit without current explicit authorization.

---

### Task 1: Add an isolated Logging behavior suite and pre-refactor allocation baseline

**Files:**
- Create: `tests/Bezoro.Logging.Tests/Bezoro.Logging.Tests.csproj`
- Create: `tests/Bezoro.Logging.Tests/README.md`
- Create: `tests/Bezoro.Logging.Tests/AssemblyInfo.cs`
- Create: `tests/Bezoro.Logging.Tests/LoggerSettingsScope.cs`
- Create: `tests/Bezoro.Logging.Tests/LoggerPayloadTests.cs`
- Create: `tests/Bezoro.Logging.Tests/LoggerFilteringTests.cs`
- Create: `tests/Bezoro.Logging.Tests/LoggerExceptionTests.cs`
- Create: `tests/Bezoro.Logging.Tests/LoggerStageTests.cs`
- Create: `benchmarks/Bezoro.Logging.Benchmarks/Bezoro.Logging.Benchmarks.csproj`
- Create: `benchmarks/Bezoro.Logging.Benchmarks/Program.cs`
- Create: `benchmarks/Bezoro.Logging.Benchmarks/LoggerAllocationBenchmarks.cs`
- Modify: `bezoro.framework.sln`

**Interfaces:**
- Tests observe only public `Logger`, `LoggerSettings`, and `LogPayload` behavior.
- Benchmark names: `LogWithoutAsyncContext` and `LogWithNestedAsyncContext`.

- [ ] **Step 1: Create the test project with repository-standard packages**

Use this project shape:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Bezoro.Logging\Bezoro.Logging.csproj" />
  </ItemGroup>
</Project>
```

`AssemblyInfo.cs` contains `[assembly: CollectionBehavior(DisableTestParallelization = true)]` because settings and the event are process-global. Add the project with:

```powershell
dotnet sln bezoro.framework.sln add tests/Bezoro.Logging.Tests/Bezoro.Logging.Tests.csproj
```

- [ ] **Step 2: Add a complete settings restore scope**

`LoggerSettingsScope` snapshots and restores `Logger.MinimumLevel`, `LoggerSettings.Enabled`, every metadata config, all five styles, stage config, grouping config, and a copy of `MutedCategories`. Its constructor sets deterministic defaults: enabled, minimum Info, sequence/timestamp/frame/thread/file/stage disabled, grouping none, and muted categories empty. `Dispose` restores all values and category contents. Keep this test helper in its own file.

- [ ] **Step 3: Characterize ordinary payload behavior**

Add public-path tests that subscribe a local `List<LogPayload>` handler in `try/finally`, call `Logger.Log`, and unsubscribe. Assert exactly one payload with unchanged raw message, level, category, context object, severity/category emoji, style, and deterministic formatted line. Configure all metadata off so this exact assertion is stable:

```csharp
payload.FormattedMessage.Should().Be("ℹ️ [🧪] hello");
```

Use the actual emoji returned by `LogLevelEmoji.GetEmoji(LogLevel.Info)` and `LogCategoryEmoji.GetEmoji(LogCategory.Test)` in the expected string if the literals differ; the assertion must remain exact, not a wildcard.

Add a formattable-string collection test and a caller/file metadata test using a fixed explicit `memberName` and `filePath` argument.

- [ ] **Step 4: Characterize filtering, exception, and stage behavior**

Add tests proving:

- disabled logging, below-minimum levels, and muted categories dispatch nothing;
- exception payloads retain `Exception`, type, stack trace, inner type/message, custom-message concatenation, and caller information;
- a unique stage name emits the ordinary payload but no divider on its first use;
- changing from one unique stage to another emits the divider before the ordinary payload and sets the stage on both;
- repeated identical stages do not emit another divider.

Use GUID-derived stage names to avoid `_lastStage` leaking between facts. Assert event order by the collected payload list.

- [ ] **Step 5: Run the green characterization suite**

```powershell
dotnet test tests/Bezoro.Logging.Tests/Bezoro.Logging.Tests.csproj --no-restore --verbosity minimal
```

Expected: all characterization tests pass against current production code. If an expectation differs, update the test to the observed, documented behavior before refactoring; do not silently change production behavior in this task.

- [ ] **Step 6: Create the allocation benchmark project**

Use `net9.0`, an executable output, `BenchmarkDotNet`, a project reference to Logging, and:

```xml
<DefineConstants>$(DefineConstants);DEBUG</DefineConstants>
```

The explicit constant is required because `[Conditional("DEBUG")]` otherwise removes calls from the Release benchmark caller. Add the project to the solution.

`LoggerAllocationBenchmarks` uses `[MemoryDiagnoser]`, subscribes a no-op sink in `[GlobalSetup]`, restores settings/unsubscribes in `[GlobalCleanup]`, and defines:

```csharp
[Benchmark(Baseline = true)]
public void LogWithoutAsyncContext() => Logger.Log("message");

[Benchmark]
public void LogWithNestedAsyncContext()
{
	using var outer = LoggerSettings.BeginAsyncContext("outer");
	using var inner = LoggerSettings.BeginAsyncContext("inner");
	Logger.Log("message");
}
```

- [ ] **Step 7: Capture the pre-refactor Release baseline**

```powershell
dotnet run -c Release --project benchmarks/Bezoro.Logging.Benchmarks/Bezoro.Logging.Benchmarks.csproj -- --filter "*LoggerAllocationBenchmarks*"
```

Expected: BenchmarkDotNet produces a report containing both exact method names and allocated bytes. Record the report path, mean, and allocation for each method in the task report.

- [ ] **Step 8: Write the test README**

Document project purpose, global-settings isolation, public payload/filter/exception/stage coverage, planned async-branch coverage, and the exact test command.

- [ ] **Step 9: Commit after explicit authorization only**

```powershell
git add tests/Bezoro.Logging.Tests benchmarks/Bezoro.Logging.Benchmarks bezoro.framework.sln
git commit -m "Add Logging behavior and allocation baselines"
```

---

### Task 2: Replace the mutable ambient stack with immutable linked context

**Files:**
- Create: `tests/Bezoro.Logging.Tests/LoggerAsyncContextTests.cs`
- Create: `src/Bezoro.Logging/Internal/AsyncContextNode.cs`
- Create: `src/Bezoro.Logging/Internal/AsyncContextScope.cs`
- Modify: `src/Bezoro.Logging/Types/LoggerSettings.cs`

**Interfaces:**
- Preserve: `LoggerSettings.BeginAsyncContext(string)` and `LogPayload.AsyncContext`, `AsyncContextDepth`, `AsyncContextHierarchy`.
- Internal ambient value: `AsyncLocal<AsyncContextNode?>`.

- [ ] **Step 1: Add nested-order and restore tests**

Add tests that log at root, child, and after child disposal. Assert exact hierarchies:

```text
["root"]
["root", "child"]
["root"]
```

Also assert `AsyncContext` is `"root > child"` and depth is `2` at the nested point.

- [ ] **Step 2: Add a deterministic parallel-branch regression test**

Within a `using` root context, start two async branches. Each branch enters its own child (`left` or `right`), signals a `TaskCompletionSource`, waits until both children are entered, then logs its branch name. Collect payloads by raw message and assert:

```csharp
byMessage["left"].AsyncContextHierarchy.Should().Equal("root", "left");
byMessage["right"].AsyncContextHierarchy.Should().Equal("root", "right");
```

Use `TaskCreationOptions.RunContinuationsAsynchronously` for gates so the failure is deterministic and not scheduler-dependent.

- [ ] **Step 3: Run the red concurrency test**

```powershell
dotnet test tests/Bezoro.Logging.Tests/Bezoro.Logging.Tests.csproj --filter "FullyQualifiedName~LoggerAsyncContextTests" --no-restore --verbosity minimal
```

Expected: the branch test fails because both execution contexts share and mutate the same `Stack<string>` instance; at least one hierarchy contains the sibling context or incorrect order.

- [ ] **Step 4: Implement immutable nodes**

`AsyncContextNode` is an internal sealed class with get-only `Name`, `Parent`, and `Depth`. Its constructor computes `Depth = (parent?.Depth ?? 0) + 1`. `ToHierarchy()` allocates one exact-length `string[]` and fills it backward from the current node to root.

`LoggerSettings` stores:

```csharp
private static readonly AsyncLocal<AsyncContextNode?> AsyncContext = new();
```

Expose internal `CurrentAsyncContextHierarchy => AsyncContext.Value?.ToHierarchy()`, plus internal get/restore methods used by the scope.

`AsyncContextScope` captures the previous node, creates/assigns one current node, and uses `Interlocked.Exchange` for idempotent disposal. On first disposal it restores the captured previous node. Nested scopes are a LIFO contract, matching `using`; do not add mutable stack state or out-of-order recovery machinery.

- [ ] **Step 5: Run async and complete Logging tests**

```powershell
dotnet test tests/Bezoro.Logging.Tests/Bezoro.Logging.Tests.csproj --filter "FullyQualifiedName~LoggerAsyncContextTests" --no-restore --verbosity minimal
dotnet test tests/Bezoro.Logging.Tests/Bezoro.Logging.Tests.csproj --no-restore --verbosity minimal
```

Expected: all tests pass; sibling contexts are isolated and nested LIFO scopes restore their parent.

- [ ] **Step 6: Commit after explicit authorization only**

```powershell
git add src/Bezoro.Logging/Internal src/Bezoro.Logging/Types/LoggerSettings.cs tests/Bezoro.Logging.Tests/LoggerAsyncContextTests.cs
git commit -m "Make Logging async contexts immutable"
```

---

### Task 3: Define immutable formatter inputs and settings snapshots

**Files:**
- Create: `src/Bezoro.Logging/Internal/LogInput.cs`
- Create: `src/Bezoro.Logging/Internal/LogSettingsSnapshot.cs`
- Create: `src/Bezoro.Logging/Internal/LogEventContext.cs`
- Create: `src/Bezoro.Logging/Internal/LogPayloadFormatter.cs`
- Create: `src/Bezoro.Logging/Properties/AssemblyInfo.cs`
- Create: `tests/Bezoro.Logging.Tests/LogPayloadFormatterTests.cs`
- Modify: `src/Bezoro.Logging/Logger.cs`

**Interfaces:**
- `LogInput` replaces the current eleven positional arguments to `BuildAndInvokePayload`.
- Formatter signature: `LogPayload Format(in LogInput input, in LogSettingsSnapshot settings, in LogEventContext context)`.
- Public `LogPayload` remains unchanged.

- [ ] **Step 1: Add internal test visibility**

Create `Properties/AssemblyInfo.cs` with:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Bezoro.Logging.Tests")]
```

This is an internal testing boundary, not a public API.

- [ ] **Step 2: Write pure formatter tests before extraction**

Add tests that construct fixed `LogInput`, snapshot, and event context values and assert an entire `LogPayload`: raw/derived fields, exact formatted message, file/caller detail line, grouping context, async string/depth/hierarchy, timestamp, stage, and style. Add a second test for exception fields and a third for a disabled-metadata main line.

Run:

```powershell
dotnet test tests/Bezoro.Logging.Tests/Bezoro.Logging.Tests.csproj --filter "FullyQualifiedName~LogPayloadFormatterTests" --no-restore --verbosity minimal
```

Expected red result: the four internal types do not exist.

- [ ] **Step 3: Add the exact immutable input record**

Create:

```csharp
internal readonly record struct LogInput(
	string Message,
	LogLevel Level,
	LogCategory? Category,
	object? ContextObject,
	Exception? Exception,
	string? ExceptionType,
	string? CallerInfo,
	string? StackTrace,
	string? InnerExceptionType,
	string? InnerExceptionMessage,
	string? FilePath);
```

Change both ordinary and exception public paths to construct this record. Replace `BuildAndInvokePayload`'s eleven parameters with one `in LogInput input` immediately, while retaining its existing body until the next step.

- [ ] **Step 4: Capture all formatting settings once per event**

`LogSettingsSnapshot` is an internal readonly record struct containing `FileLocationConfig`, `FrameCountConfig`, `GroupingConfig`, `SequenceNumberConfig`, `ThreadIdConfig`, `TimestampConfig`, and all five `LogStyle` references. Its static `Capture()` reads each `LoggerSettings` property exactly once. Its `GetStyle(LogLevel)` switch matches current behavior.

`LogEventContext` is an internal readonly record struct containing:

```csharp
DateTime Timestamp,
long SequenceNumber,
int ThreadId,
int? FrameCount,
IReadOnlyList<string>? AsyncHierarchy,
string? Stage
```

`Logger` invokes the captured frame provider once and puts the result in `FrameCount`; the formatter never calls mutable global state or a delegate.

- [ ] **Step 5: Extract the pure formatter**

Move these responsibilities from `Logger` into `LogPayloadFormatter`: severity/category emoji lookup, grouping computation, file-location selection, async string/depth, metadata/main/detail line construction, and `LogPayload` initialization. Every helper must take only its arguments, snapshot, or event context. No formatter method may read `LoggerSettings`, `Logger.MinimumLevel`, `_lastStage`, the clock, thread environment, sequence state, or `OnLog`.

Keep collection/formattable input conversion and caller-name extraction in `Logger` because they shape `LogInput` before filtering/formatting. Keep filtering, stage transition, sequence increment, timestamp/thread/frame capture, and `OnLog?.Invoke` in `Logger`.

- [ ] **Step 6: Make the Logger orchestration explicit**

After filtering and stage-transition detection, `Logger` captures one settings snapshot and async hierarchy. If a divider is required, it creates a divider `LogEventContext` with its own `DateTime.UtcNow`, current thread ID, `SequenceNumber = 0`, `FrameCount = null`, the captured hierarchy, and normalized stage; formats and dispatches the divider without incrementing the sequence or invoking the frame provider. It then executes this ordinary-payload order:

```csharp
var settings = LogSettingsSnapshot.Capture();
var asyncHierarchy = LoggerSettings.CurrentAsyncContextHierarchy;
var timestamp = DateTime.UtcNow;
var context = new LogEventContext(
	timestamp,
	LoggerSettings.GetNextSequenceNumber(),
	Environment.CurrentManagedThreadId,
	settings.FrameCount.Enabled && settings.FrameCount.Provider is not null
		? settings.FrameCount.Provider()
		: null,
	asyncHierarchy,
	normalizedStage);
var payload = LogPayloadFormatter.Format(input, settings, context);
OnLog?.Invoke(payload);
```

The formatter must also expose `FormatStageDivider(string dividerLine, in LogInput input, in LogSettingsSnapshot settings, in LogEventContext context)` as a pure method. `Logger` remains responsible for deciding whether and when to dispatch it.

- [ ] **Step 7: Run formatter and public behavior suites**

```powershell
dotnet test tests/Bezoro.Logging.Tests/Bezoro.Logging.Tests.csproj --filter "FullyQualifiedName~LogPayloadFormatterTests|FullyQualifiedName~LoggerPayloadTests|FullyQualifiedName~LoggerExceptionTests|FullyQualifiedName~LoggerStageTests" --no-restore --verbosity minimal
```

Expected: pure formatter tests and every pre-refactor characterization test pass with exact payload parity.

- [ ] **Step 8: Commit after explicit authorization only**

```powershell
git add src/Bezoro.Logging/Internal src/Bezoro.Logging/Properties/AssemblyInfo.cs src/Bezoro.Logging/Logger.cs tests/Bezoro.Logging.Tests/LogPayloadFormatterTests.cs
git commit -m "Extract immutable Logging payload formatting"
```

---

### Task 4: Prove orchestration, snapshot, and concurrency boundaries after extraction

**Files:**
- Create: `tests/Bezoro.Logging.Tests/LoggerSettingsSnapshotTests.cs`
- Modify: `tests/Bezoro.Logging.Tests/LoggerPayloadTests.cs`
- Modify: `tests/Bezoro.Logging.Tests/LoggerStageTests.cs`
- Modify: `tests/Bezoro.Logging.Tests/LoggerAsyncContextTests.cs`
- Modify: `src/Bezoro.Logging/Logger.cs`
- Modify: `src/Bezoro.Logging/Internal/LogPayloadFormatter.cs`

**Interfaces:**
- Logger owns side effects exactly once per accepted ordinary payload.
- Formatter owns no side effects.

- [ ] **Step 1: Add single-capture tests**

Configure a frame provider that increments a counter and returns `42`. Emit one accepted log and assert the provider runs once and the formatted metadata includes `F42`. Emit a filtered log and assert the provider does not run. Add a custom mutable config replacement during `OnLog` and assert the already-built payload reflects the pre-dispatch snapshot.

- [ ] **Step 2: Add event ordering and subscriber-context tests**

Capture `Environment.CurrentManagedThreadId` before `Logger.Log` and inside the handler; assert equality. Keep stage divider before ordinary payload. This proves the refactor did not introduce a worker thread, callback context, or queue.

- [ ] **Step 3: Run the tests and fix only orchestration defects**

```powershell
dotnet test tests/Bezoro.Logging.Tests/Bezoro.Logging.Tests.csproj --filter "FullyQualifiedName~LoggerSettingsSnapshotTests|FullyQualifiedName~LoggerPayloadTests|FullyQualifiedName~LoggerStageTests|FullyQualifiedName~LoggerAsyncContextTests" --no-restore --verbosity minimal
```

Expected: all pass. If a new test fails, fix capture/dispatch ordering in `Logger`; do not move state reads back into the formatter.

- [ ] **Step 4: Verify forbidden dependencies are absent**

```powershell
rg -n 'LoggerSettings|DateTime\.(UtcNow|Now)|Environment\.CurrentManagedThreadId|GetNextSequenceNumber|OnLog' src/Bezoro.Logging/Internal/LogPayloadFormatter.cs
```

Expected: no matches. `LogPayloadFormatter` is a deterministic transform of its three inputs.

- [ ] **Step 5: Commit after explicit authorization only**

```powershell
git add src/Bezoro.Logging tests/Bezoro.Logging.Tests
git commit -m "Verify Logging orchestration boundaries"
```

---

### Task 5: Correct Logging documentation, remove unused Core coupling, and compare allocations

**Files:**
- Modify: `src/Bezoro.Logging/README.md`
- Modify: `src/Bezoro.Logging/Bezoro.Logging.csproj`
- Modify: `benchmarks/Bezoro.Logging.Benchmarks/LoggerAllocationBenchmarks.cs` only if setup parity requires it
- Verify: `benchmarks/Bezoro.Logging.Benchmarks/BenchmarkDotNet.Artifacts/results/*`

**Interfaces:**
- Preserve: no public Logging API additions/removals/deprecations.
- Remove: unused `Bezoro.Logging` project reference to `Bezoro.Core`.

- [ ] **Step 1: Fix README examples against compiled APIs**

Make these exact corrections:

```csharp
LoggerSettings.FileLocation = FileLocationConfig.FilenameOnly;
LoggerSettings.Grouping = GroupingConfig.Create(LoggerSettings.ContextGrouping.CallerType);
LoggerSettings.Grouping = GroupingConfig.Create(LoggerSettings.ContextGrouping.Category);
LoggerSettings.Grouping = GroupingConfig.Create(LoggerSettings.ContextGrouping.Thread);
LoggerSettings.Grouping = GroupingConfig.Create(LoggerSettings.ContextGrouping.AsyncContext);
LoggerSettings.Grouping = GroupingConfig.Create(LoggerSettings.ContextGrouping.TimeWindow, timeWindowMs: 1000);
```

Remove references to nonexistent `FileLocationConfig.FileName`, `GroupingConfig.By`, and `GroupingConfig.ByTimeWindow`. Change the performance claims from “zero-allocation”/“zero-overhead” to factual conditional-call-site and measured-allocation language. Describe immutable branch-safe async contexts and the Logger/formatter ownership split.

- [ ] **Step 2: Prove and remove the unused Core reference**

```powershell
rg -n --glob '*.cs' 'Bezoro\.Core|using Bezoro\.Core' src/Bezoro.Logging
```

Expected: no matches. Remove:

```xml
<ProjectReference Include="..\Bezoro.Core\Bezoro.Core.csproj" />
```

from `src/Bezoro.Logging/Bezoro.Logging.csproj`, and remove the README dependency row/link.

- [ ] **Step 3: Build Logging independently for both targets**

```powershell
dotnet build src/Bezoro.Logging/Bezoro.Logging.csproj -f net9.0 --no-restore --verbosity minimal
dotnet build src/Bezoro.Logging/Bezoro.Logging.csproj -f netstandard2.1 --no-restore --verbosity minimal
```

Expected: both builds pass with zero warnings and no transitive Core build requirement.

- [ ] **Step 4: Capture the post-refactor allocation report**

```powershell
dotnet run -c Release --project benchmarks/Bezoro.Logging.Benchmarks/Bezoro.Logging.Benchmarks.csproj -- --filter "*LoggerAllocationBenchmarks*"
```

Expected: the same two benchmark names complete. Compare their mean and allocated bytes with Task 1. Accept the refactor only when `LogWithoutAsyncContext` has no unexplained material regression and `LogWithNestedAsyncContext` demonstrates the expected removal of mutable-stack copy/enumeration cost or the task report explicitly records why immutable scope allocation offsets it. Do not state “zero allocation” unless the report says `0 B`.

- [ ] **Step 5: Commit after explicit authorization only**

```powershell
git add src/Bezoro.Logging/README.md src/Bezoro.Logging/Bezoro.Logging.csproj benchmarks/Bezoro.Logging.Benchmarks
git commit -m "Document and measure Logging simplification"
```

---

### Task 6: Complete Logging compatibility and repository verification

**Files:**
- Verify: `api/PublicTypes.Bezoro.Logging.txt`
- Verify: `bezoro.framework.sln`
- Verify: every file changed by Tasks 1-5

**Interfaces:**
- Produce: unchanged public Logging type baseline, dual-target builds, passing tests, and before/after benchmark evidence.

- [ ] **Step 1: Verify public shape and internal ownership**

```powershell
git diff -- src/Bezoro.Logging/Logger.cs src/Bezoro.Logging/Types/LogPayload.cs src/Bezoro.Logging/Types/LoggerSettings.cs
rg -n 'AsyncLocal<Stack|Stack<string>|BuildAndInvokePayload\(\s*string' src/Bezoro.Logging
rg -n 'public ' src/Bezoro.Logging/Internal
```

Expected: `LogPayload.cs` is unchanged; public logger signatures/attributes remain unchanged; mutable ambient stacks and the eleven-argument helper are absent; internal files declare no public type.

- [ ] **Step 2: Verify the public API baseline**

```powershell
./scripts/Export-PublicApi.ps1
git diff -- api/PublicTypes.Bezoro.Logging.txt
./scripts/Export-PublicApi.ps1 -Check
```

Expected: the Logging baseline has no diff because all new types are internal and no public type changed.

- [ ] **Step 3: Run focused and full tests**

```powershell
dotnet test tests/Bezoro.Logging.Tests/Bezoro.Logging.Tests.csproj --no-restore --verbosity minimal
dotnet test tests/Bezoro.Chess.UCI.Protocol.Tests/Bezoro.Chess.UCI.Protocol.Tests.csproj --no-restore --verbosity minimal
dotnet test tests/Bezoro.Chess.UCI.Tests/Bezoro.Chess.UCI.Tests.csproj --no-restore --verbosity minimal
dotnet test bezoro.framework.sln --no-restore --verbosity minimal
```

Expected: all tests pass with zero warnings; protocol/Chess consumers observe unchanged logging behavior.

- [ ] **Step 4: Run both solution builds and inspect the diff**

```powershell
dotnet build bezoro.framework.sln --no-restore --verbosity minimal
dotnet build bezoro.framework.sln -c Release --no-restore --verbosity minimal
git diff --check
git status --short
```

Expected: all declared targets compile with zero warnings/errors; diff check is clean; status contains only authorized changes plus pre-existing plans/spec files.

- [ ] **Step 5: Self-review against the approved design**

Record direct evidence for: pre-refactor behavior/concurrency tests; immutable linked ambient nodes; prior-context restoration; eleven positional arguments replaced by `LogInput`; one settings capture; pure formatter; Logger-owned filter/stage/sequence/dispatch; callback context unchanged; public logger/payload shape unchanged; README corrections; no Core edge; both allocation baselines; both targets; full tests/build/API checks.

- [ ] **Step 6: Commit after explicit authorization only**

```powershell
git add tests/Bezoro.Logging.Tests/README.md bezoro.framework.sln
git commit -m "Complete Logging simplification verification"
```
