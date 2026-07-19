# Coding Workflow Conformance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve every audited coding-workflow and repository-convention gap without changing public APIs, and correct any concrete runtime defect exposed by verification through TDD.

**Architecture:** Preserve the existing project and runtime boundaries. Apply mechanical one-type-per-file moves, explicit private buffer setup, accurate hot-path rationale, documentation completion, and removal of redundant warning suppressions. Serialize the UCI ponder search lifecycle at its existing internal boundary after a deterministic test reproduces the protocol race. Validate compiled identity through the existing public API baselines and behavior through targeted and full test suites.

**Tech Stack:** C#/.NET 10 SDK, multi-targeted `net9.0` and `netstandard2.1`, xUnit, FluentAssertions, PowerShell, existing public API export script.

## Global Constraints

- Preserve all public symbols, namespaces, signatures, defaults, exceptions, and serialized shapes.
- Keep the framework engine-agnostic and retain existing ECS hot-path specialization.
- Keep one top-level type per source file.
- Preserve local tabs, LF line endings, file-scoped namespaces, nullable correctness, and warnings-as-errors.
- Do not stage, commit, broadly format, or modify unrelated files.

---

### Task 1: Project Metadata And Required Documentation

**Files:**
- Modify: `src/Bezoro.Chess.UCI/Bezoro.Chess.UCI.csproj`
- Modify: `src/Bezoro.ECS.SourceGen/Bezoro.ECS.SourceGen.csproj`
- Modify: `src/Bezoro.ECS/Bezoro.ECS.csproj`
- Modify: `src/Bezoro.GameSystems/Bezoro.GameSystems.csproj`
- Modify: `src/Bezoro.Chess.UCI/API/Common/Enums/PieceColor.cs`
- Modify: `src/Bezoro.Chess.UCI/API/Common/Enums/PieceType.cs`
- Modify: `src/Bezoro.Chess.UCI/API/Common/Extensions/CharExtensions.cs`
- Modify: `src/Bezoro.Chess.UCI/API/Common/Extensions/PieceTypeExtensions.cs`
- Modify: `src/Bezoro.Chess.UCI/API/Types/BoardState.cs`
- Modify: `src/Bezoro.Chess.UCI/API/Types/Move.cs`
- Modify: `src/Bezoro.Chess.UCI/API/Types/MoveAnalysis.cs`
- Modify: `src/Bezoro.Chess.UCI/API/Types/MoveScore.cs`
- Modify: `src/Bezoro.Chess.UCI/API/Types/MovesSnapshot.cs`
- Modify: `src/Bezoro.Chess.UCI/API/Types/MoveToPieceMap.cs`
- Modify: `src/Bezoro.Chess.UCI/API/Types/ParsedMove.cs`
- Modify: `src/Bezoro.Chess.UCI/API/Types/Position.cs`
- Modify: `src/Bezoro.ECS.SourceGen/Generators/ComponentCatalogGenerator.cs`
- Modify: `src/Bezoro.ECS.SourceGen/Generators/ForEachJobSourceGenerator.cs`
- Modify: `src/Bezoro.ECS.SourceGen/Generators/QuerySourceGenerator.cs`
- Modify: `src/Bezoro.ECS.SourceGen/Generators/SplitFieldSourceGenerator.cs`
- Modify: `src/Bezoro.ECS.SourceGen/Generators/SystemMetadataGenerator.cs`
- Modify: `src/Bezoro.Core/README.md`
- Modify: `src/Bezoro.ECS.SourceGen/README.md`
- Modify: `src/Bezoro.GameSystems/README.md`
- Modify: `src/Bezoro.Logging/README.md`
- Modify: `src/Bezoro.TypingSystem/README.md`
- Create: `tests/Bezoro.GameSystems.Tests/README.md`

**Interfaces:**
- Consumes: existing project public types and examples.
- Produces: warning-clean projects with complete project documentation; no compiled contract changes.

- [ ] **Step 1: Remove redundant CS1591 suppression**

Delete the following element from each listed project:

```xml
<NoWarn>$(NoWarn);1591</NoWarn>
```

- [ ] **Step 2: Complete README contract**

Ensure every listed source README contains concrete project purpose, key types, quick-start code, API reference, feature-specific notes, and design notes. Create the GameSystems test README with its test areas, quick-start command, guarantees, and conventions. Use only types and commands that exist in the current repository.

- [ ] **Step 3: Complete the unsuppressed XML documentation contract**

Add meaningful `///` documentation to all public declarations reported by CS1591 across Chess UCI, ECS, GameSystems, and the five source-generator files. Document each type and enum member, and include `<param>`, `<returns>`, and `<exception>` where the signature or implementation requires them. Do not change executable statements or public signatures.

- [ ] **Step 4: Verify metadata and documentation changes**

Run:

```powershell
dotnet build src/Bezoro.Chess.UCI/Bezoro.Chess.UCI.csproj --no-restore --verbosity minimal
dotnet build src/Bezoro.ECS/Bezoro.ECS.csproj --no-restore --verbosity minimal
dotnet build src/Bezoro.ECS.SourceGen/Bezoro.ECS.SourceGen.csproj --no-restore --verbosity minimal
dotnet build src/Bezoro.GameSystems/Bezoro.GameSystems.csproj --no-restore --verbosity minimal
```

Expected: all four builds succeed with zero warnings and zero errors.

### Task 2: Core, Typing, And Chess Type Organization

**Files:**
- Modify: `src/Bezoro.Core/Types/ResultFactory.cs`
- Create: `src/Bezoro.Core/Types/IFailureReason.cs`
- Create: `src/Bezoro.Core/Types/Result.cs`
- Modify: `src/Bezoro.Core/CodeGen/CodeWriter.cs`
- Create: `src/Bezoro.Core/CodeGen/CSharpFileGenerator.cs`
- Modify: `src/Bezoro.Core/Helpers/ArrayHelper.cs`
- Create: `src/Bezoro.Core/Helpers/ArrayIsFullException.cs`
- Modify: `src/Bezoro.Core/Types/GridSpan2D.cs`
- Create: `src/Bezoro.Core/Types/ReadOnlyGridSpan2D.cs`
- Modify: `src/Bezoro.TypingSystem/Types/TypingResult.cs`
- Create: `src/Bezoro.TypingSystem/Types/TypingValidationStatus.cs`
- Modify: `src/Bezoro.Chess.UCI/API/Types/ParsedMove.cs`
- Create: `src/Bezoro.Chess.UCI/API/Types/Promotion.cs`

**Interfaces:**
- Consumes: the existing declarations in each multi-type source file.
- Produces: identical compiled types, each declared in its own file.

- [ ] **Step 1: Move declarations verbatim**

Use this exact mapping; preserve attributes, XML docs, generic constraints, interfaces, members, namespace, and accessibility byte-for-byte apart from surrounding blank lines:

| Source file | Declaration moved | Destination file |
| --- | --- | --- |
| `ResultFactory.cs` | `IFailureReason` | `IFailureReason.cs` |
| `ResultFactory.cs` | `Result<T>` | `Result.cs` |
| `CodeWriter.cs` | `CSharpFileGenerator` | `CSharpFileGenerator.cs` |
| `ArrayHelper.cs` | `ArrayIsFullException` | `ArrayIsFullException.cs` |
| `GridSpan2D.cs` | `ReadOnlyGridSpan2D<T>` | `ReadOnlyGridSpan2D.cs` |
| `TypingResult.cs` | `TypingValidationStatus` | `TypingValidationStatus.cs` |
| `ParsedMove.cs` | `Promotion` | `Promotion.cs` |

- [ ] **Step 2: Verify affected behavior and contracts**

Run:

```powershell
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj --no-restore --verbosity minimal
dotnet test tests/Bezoro.TypingSystem.Tests/Bezoro.TypingSystem.Tests.csproj --no-restore --verbosity minimal
dotnet test tests/Bezoro.Chess.UCI.Tests/Bezoro.Chess.UCI.Tests.csproj --no-restore --verbosity minimal
./scripts/Export-PublicApi.ps1 -Check
```

Expected: all tests and the public API check pass.

### Task 3: GameSystems Type Organization

**Files:**
- Modify: `src/Bezoro.GameSystems/ActivationSystem/Services/ActivationProcessingSystem.cs`
- Create: `src/Bezoro.GameSystems/ActivationSystem/Services/ActivationEntryQuery.cs`
- Create: `src/Bezoro.GameSystems/ActivationSystem/Services/ActivationCancellationQuery.cs`
- Modify: `src/Bezoro.GameSystems/HealthSystem/Services/HealthSystem.cs`
- Create: `src/Bezoro.GameSystems/HealthSystem/Services/HealthMutationRequestQuery.cs`
- Modify: `src/Bezoro.GameSystems/StreamingSystem/Services/StreamingSystem.cs`
- Create: `src/Bezoro.GameSystems/StreamingSystem/Services/StreamingQuery.cs`
- Modify: `src/Bezoro.GameSystems/TimerSystem/Services/TimerSystem.cs`
- Create: `src/Bezoro.GameSystems/TimerSystem/Services/TimerQuery.cs`

**Interfaces:**
- Consumes: existing source-generator query declarations.
- Produces: identical generated query contracts with one top-level type per file.

- [ ] **Step 1: Move query declarations verbatim**

| Source file | Declaration moved | Destination file |
| --- | --- | --- |
| `ActivationProcessingSystem.cs` | `ActivationEntryQuery` | `ActivationEntryQuery.cs` |
| `ActivationProcessingSystem.cs` | `ActivationCancellationQuery` | `ActivationCancellationQuery.cs` |
| `HealthSystem.cs` | `HealthMutationRequestQuery` | `HealthMutationRequestQuery.cs` |
| `StreamingSystem.cs` | `StreamingQuery` | `StreamingQuery.cs` |
| `TimerSystem.cs` | `TimerQuery` | `TimerQuery.cs` |

- [ ] **Step 2: Verify source generation and gameplay behavior**

Run:

```powershell
dotnet test tests/Bezoro.GameSystems.Tests/Bezoro.GameSystems.Tests.csproj --no-restore --verbosity minimal
```

Expected: all GameSystems tests pass and generated query types compile.

### Task 4: ECS Type Organization And Explicit Hot-Path Intent

**Files:**
- Modify: `src/Bezoro.ECS/Internal/Fixed/ArchetypeTypeSetKey.cs`
- Create: `src/Bezoro.ECS/Internal/Fixed/ArchetypeTypeSetKeyComparer.cs`
- Modify: `src/Bezoro.ECS/Internal/QueryChunkWalker.cs`
- Create: `src/Bezoro.ECS/Internal/IChunkAction.Arity1.cs`
- Create: `src/Bezoro.ECS/Internal/IChunkAction.Arity2.cs`
- Create: `src/Bezoro.ECS/Internal/IChunkAction.Arity3.cs`
- Create: `src/Bezoro.ECS/Internal/IChunkAction.Arity4.cs`
- Create: `src/Bezoro.ECS/Internal/IEntityChunkAction.Arity1.cs`
- Create: `src/Bezoro.ECS/Internal/IEntityChunkAction.Arity2.cs`
- Create: `src/Bezoro.ECS/Internal/IEntityChunkAction.Arity3.cs`
- Create: `src/Bezoro.ECS/Internal/IEntityChunkAction.Arity4.cs`
- Modify: `src/Bezoro.ECS/Types/CommandStream.cs`
- Modify: `src/Bezoro.ECS/Services/World.cs`

**Interfaces:**
- Consumes: existing internal generic action contracts and command playback behavior.
- Produces: unchanged internal compiled identities, explicit buffer allocation paths, and accurate architectural/performance rationale.

- [ ] **Step 1: Move ECS declarations verbatim**

Move `ArchetypeTypeSetKeyComparer` and each of the eight arity-specific action interfaces to the exact destination files above. Keep `QueryChunkWalker` as the only top-level type in its file.

- [ ] **Step 2: Make buffer setup explicit**

Replace call sites as follows:

```csharp
// Remove playback
EnsureBatchEntityBuffers();

// Set playback
EnsureBatchEntityBuffers();
EnsureBatchPayloadIndexBuffer();
```

Replace the boolean helper with:

```csharp
private void EnsureBatchEntityBuffers()
{
	if (_batchEntityIds is null)
		_batchEntityIds = ArrayPool<int>.Shared.Rent(_commandCapacity);

	if (_batchEntityMarkerBits is not null && _batchTouchedMarkerWordIndices is not null)
		return;

	int markerWordCount = (Owner.EntityCapacity + 31) >> 5;
	_batchEntityMarkerBits = ArrayPool<uint>.Shared.Rent(markerWordCount);
	Array.Clear(_batchEntityMarkerBits, 0, markerWordCount);
	_batchTouchedMarkerWordIndices = ArrayPool<int>.Shared.Rent(markerWordCount);
	_batchTouchedMarkerWordCount   = 0;
}

private void EnsureBatchPayloadIndexBuffer()
{
	if (_batchPayloadIndices is null)
		_batchPayloadIndices = ArrayPool<int>.Shared.Rent(_commandCapacity);
}
```

- [ ] **Step 3: Replace stale TODOs with rationale**

Use this façade rationale above `World`:

```csharp
// World is the stable public façade. Storage, queries, lifecycle, snapshots, resources, and system
// execution remain delegated to focused internal services while this type preserves the ergonomic API.
```

Use this hot-path rationale above `QueryChunkWalker`:

```csharp
// The arity-specific loops are intentionally specialized. They keep component access statically typed
// and avoid per-entity dispatch or temporary collections on this benchmarked ECS hot path.
```

- [ ] **Step 4: Verify ECS behavior**

Run:

```powershell
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj --no-restore --verbosity minimal
```

Expected: all ECS tests pass.

### Task 4A: UCI Search Lifecycle Reliability

**Files:**
- Modify: `src/Bezoro.Chess.UCI.Protocol/Internal/UciPonderRuntime.cs`
- Create: `tests/Bezoro.Chess.UCI.Protocol.Tests/Domain/UciPonderRuntimeTests.cs`

**Interfaces:**
- Consumes: the existing internal ponder runtime and protocol transport seam.
- Produces: deterministic StartSearch/StopSearch ordering without public API changes.

- [ ] **Step 1: Reproduce the race deterministically**

Block the `position` write in a mock transport, invoke `StopSearchAsync`, and prove that the unfixed runtime emits `stop` before the `position`/`go` start sequence completes.

- [ ] **Step 2: Serialize search lifecycle operations**

Guard the complete start and stop sequences with one internal semaphore. Extract a non-locking stop core so restarting an active search cannot recursively acquire the gate.

- [ ] **Step 3: Verify the regression and integration behavior**

Run the deterministic protocol regression and the existing Stockfish-backed concurrent StartSearch/StopSearch test. Expected: both pass consistently.

### Task 5: Completion Audit And Full Verification

**Files:**
- Inspect: all files changed by Tasks 1-4.
- Inspect: `api/PublicTypes.*.txt` through the existing check script.

**Interfaces:**
- Consumes: all prior task outputs.
- Produces: evidence for every 10/10 exit criterion.

- [ ] **Step 1: Rerun objective structural audits**

Run searches proving:

```powershell
rg -n --glob '*.csproj' '<NoWarn>.*1591' src
rg -n --glob '*.cs' 'TODO: \[CODE SMELL' src
```

Expected: no matches.

Run the top-level declaration grouping audit used during review and verify that every reported source file contains only one top-level type.

- [ ] **Step 2: Verify README coverage**

Enumerate every project under `src/` and `tests/` and verify a sibling `README.md` exists. Inspect all changed READMEs for the required purpose, key types, quick start, API reference or test guarantees, feature notes, and design notes.

- [ ] **Step 3: Verify compatibility and behavior**

Run:

```powershell
./scripts/Export-PublicApi.ps1 -Check
dotnet test bezoro.framework.sln --verbosity minimal
dotnet build bezoro.framework.sln
```

Expected: API check exit code 0; all 1,777 tests pass, including the new UCI ordering regression; build succeeds with zero warnings and zero errors across all targets.

- [ ] **Step 4: Inspect final diff**

Run:

```powershell
git diff --check
git status --short
git diff --stat
git diff
```

Expected: no whitespace errors, no unrelated changes, and only the approved conformance work remains.
