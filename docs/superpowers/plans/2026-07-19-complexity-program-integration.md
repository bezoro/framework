# Complexity Simplification Program Integration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate the approved subsystem simplification plans into one compatibility-preserving repository change, reduce unnecessary project coupling, publish one migration guide, and produce complete verification evidence for the final score.

**Architecture:** Domain plans own behavior and public API changes inside their source projects. This program plan owns cross-project dependency direction, execution order, migration documentation, full-solution gates, and final before/after evidence. It does not duplicate subsystem implementation tasks.

**Tech Stack:** C#/.NET 9, .NET Standard 2.1, Roslyn source generators on .NET Standard 2.0, MSBuild, xUnit, FluentAssertions, BenchmarkDotNet, PowerShell.

## Global Constraints

- Keep every superseded public member or type available with `[Obsolete("Use ... instead.")]`; obsolete-code removal is deferred until downstream migration is complete.
- Do not remove public, internal, benchmark, sample, or test capability solely because no current repository reference exists.
- Require red-green-refactor TDD for behavior and public API changes; configuration-only dependency edits use build evidence instead.
- Target `net9.0` and `netstandard2.1`; keep `Bezoro.ECS.SourceGen` on `netstandard2.0`.
- Treat nullable and analyzer warnings as errors and generate XML documentation for public APIs.
- Preserve serialized and reflection-visible shapes unless an explicit compatibility test proves the change.
- Keep namespaces aligned with folders and one top-level type per source file.
- Preserve data-oriented ECS kernels, UCI protocol behavior, concurrency ownership, and benchmarked hot-path characteristics.
- Update project READMEs, samples, XML documentation, public API baselines, and the migration guide with public changes.
- Do not stage, commit, push, alter the existing pull request, publish, or deploy without current explicit user authorization.

---

### Task 1: Establish the target project dependency graph

**Files:**
- Modify: `src/Bezoro.GameSystems/Bezoro.GameSystems.csproj`
- Verify: `src/Bezoro.Logging/Bezoro.Logging.csproj`
- Verify: `src/Bezoro.TypingSystem/Bezoro.TypingSystem.csproj`
- Verify: `src/Bezoro.Chess.UCI.Protocol/Bezoro.Chess.UCI.Protocol.csproj`
- Verify: `src/Bezoro.Chess.UCI/Bezoro.Chess.UCI.csproj`

**Interfaces:**
- Consumes: project-local dependency removals from the Core/Typing/Logging and Chess/UCI plans.
- Produces: a source-project graph with only demonstrated compile-time edges: Core; Events; Logging; Typing → Core; ECS → Core + SourceGen analyzer; GameSystems → Core + ECS + SourceGen analyzer; Protocol → Logging; Chess.UCI → Protocol + Core.

- [ ] **Step 1: Record the current source dependency edges**

Run:

```powershell
rg -n --glob '*.csproj' '<ProjectReference|<PackageReference' src
```

Expected: the output includes the existing `Bezoro.GameSystems` → `Bezoro.Events` project reference and the other candidate edges listed in the approved design.

- [ ] **Step 2: Prove the GameSystems Events reference carries no source-level capability**

Run:

```powershell
rg -n --glob '*.cs' 'using Bezoro\.Events|\bIEventBus\b|\bEventBus\b|\bUnityEventBuses\b' src/Bezoro.GameSystems
```

Expected: no matches. If a real symbol use exists, retain the reference and record the symbol in the task report rather than removing capability.

- [ ] **Step 3: Remove only the unused GameSystems Events declaration**

Delete this XML element from `src/Bezoro.GameSystems/Bezoro.GameSystems.csproj`:

```xml
<ProjectReference Include="..\Bezoro.Events\Bezoro.Events.csproj" />
```

Do not change source under `src/Bezoro.Events`.

- [ ] **Step 4: Build and test the affected projects**

Run:

```powershell
dotnet build src/Bezoro.GameSystems/Bezoro.GameSystems.csproj --no-restore --verbosity minimal
dotnet test tests/Bezoro.GameSystems.Tests/Bezoro.GameSystems.Tests.csproj --no-restore --verbosity minimal
```

Expected: both target frameworks build with zero warnings/errors and all GameSystems tests pass.

- [ ] **Step 5: Verify the final graph after every domain plan completes**

Run:

```powershell
rg -n --glob '*.csproj' '<ProjectReference|<PackageReference' src
```

Expected source-project edges:

```text
Bezoro.Core: none
Bezoro.Events: none
Bezoro.Logging: none
Bezoro.TypingSystem: Bezoro.Core
Bezoro.ECS.SourceGen: Roslyn analyzer packages only
Bezoro.ECS: Bezoro.Core; Bezoro.ECS.SourceGen as analyzer; System.Text.Json only on netstandard2.1
Bezoro.GameSystems: Bezoro.Core; Bezoro.ECS; Bezoro.ECS.SourceGen as analyzer
Bezoro.Chess.UCI.Protocol: Bezoro.Logging; System.Collections.Immutable; System.Threading.Channels only on netstandard2.1
Bezoro.Chess.UCI: Bezoro.Chess.UCI.Protocol; Bezoro.Core; System.Collections.Immutable; System.Threading.Channels only on netstandard2.1
```

The direct Chess.UCI → Core edge remains because `ParsedMove` and `Promotion` expose Core validation exception behavior; removing it would change a public contract. Any additional edge must have an identified source symbol or build capability in the task report.

- [ ] **Step 6: Commit after explicit authorization only**

```powershell
git add src/Bezoro.GameSystems/Bezoro.GameSystems.csproj
git commit -m "Reduce source project coupling"
```

Expected: one configuration-only commit. Skip this step unless current user authorization permits staging and committing.

---

### Task 2: Execute subsystem plans in risk order

**Files:**
- Read: `docs/superpowers/plans/2026-07-19-core-complexity-simplification.md`
- Read: `docs/superpowers/plans/2026-07-19-typing-api-simplification.md`
- Read: `docs/superpowers/plans/2026-07-19-logging-complexity-simplification.md`
- Read: `docs/superpowers/plans/2026-07-19-ecs-complexity-simplification.md`
- Read: `docs/superpowers/plans/2026-07-19-chess-uci-complexity-simplification.md`

**Interfaces:**
- Consumes: complete, self-reviewed subsystem plans.
- Produces: a deterministic execution order in which lower-level API migrations stabilize before dependent projects migrate.

- [ ] **Step 1: Execute the Core plan**

Use `superpowers:subagent-driven-development` on every Core task, including its task-scoped implementation and review loop.

Expected: Core canonical APIs and compatibility shims are complete before dependent projects migrate.

- [ ] **Step 2: Execute the Typing plan**

Expected: Typing uses canonical Core APIs and its own canonical result/word-source APIs; focused tests and docs pass.

- [ ] **Step 3: Execute the Logging plan**

Expected: Logging has an isolated test project, immutable ambient contexts, and a cohesive internal payload flow before transport projects are verified.

- [ ] **Step 4: Execute the ECS plan**

Expected: canonical access/query APIs, source-generator inference, direct traversal, and benchmark gates pass before GameSystems is migrated.

- [ ] **Step 5: Migrate and verify GameSystems against canonical ECS/Core APIs**

Run:

```powershell
rg -n --glob '*.cs' '\.(Get|GetResource|TryGetManaged)<' src/Bezoro.GameSystems tests/Bezoro.GameSystems.Tests
dotnet test tests/Bezoro.GameSystems.Tests/Bezoro.GameSystems.Tests.csproj --no-restore --verbosity minimal
```

Expected: no use of ECS members deprecated by this program and all GameSystems tests pass.

- [ ] **Step 6: Execute the Chess/UCI plan**

Expected: protocol and coordinator changes build on stabilized Core/Logging behavior, pass concurrency tests, and include current benchmark evidence.

- [ ] **Step 7: Confirm every task review is clean**

Read `.superpowers/sdd/progress.md` and each task report.

Expected: every subsystem task is recorded complete with spec-compliance approval, code-quality approval, covering tests, and no unresolved Critical or Important review item.

---

### Task 3: Publish a single canonical API migration guide

**Files:**
- Create: `docs/migrations/2026-07-19-api-simplification.md`
- Modify: project READMEs only when they need a link to the shared guide.

**Interfaces:**
- Consumes: final canonical and obsolete public signatures from every subsystem plan.
- Produces: one consumer migration sequence and a later obsolete-removal checklist.

- [ ] **Step 1: Write the migration guide from compiled signatures**

Create `docs/migrations/2026-07-19-api-simplification.md` with this exact structure and replace each table row with the compiled old/new signature and behavior notes from the completed task reports:

```markdown
# API Simplification Migration Guide

## Compatibility Window

All superseded APIs remain available with `[Obsolete]` guidance in this release. Removal is deferred until downstream consumers have migrated and a later breaking-release cleanup is explicitly authorized.

## Core

| Obsolete API | Canonical API | Migration note |
| --- | --- | --- |

## Typing

| Obsolete API | Canonical API | Migration note |
| --- | --- | --- |

## ECS

| Obsolete API | Canonical API | Migration note |
| --- | --- | --- |

## Chess and UCI

| Obsolete API | Canonical API | Migration note |
| --- | --- | --- |

## Removal Checklist for a Later Breaking Release

- Confirm downstream builds contain no obsolete warnings from Bezoro.Framework.
- Remove compatibility members and their compatibility-only tests.
- Remove obsolete documentation rows and aliases from API baselines.
- Re-run the complete test, build, API, structural, and benchmark gates.
```

Expected: every `[Obsolete]` declaration introduced by this program appears exactly once in the guide, grouped by assembly.

- [ ] **Step 2: Verify guide coverage against declarations**

Run:

```powershell
$obsolete = rg -n --glob '*.cs' '\[Obsolete\(' src
$obsolete
rg -n '\| .*\| .*\|' docs/migrations/2026-07-19-api-simplification.md
```

Expected: manual comparison proves every introduced obsolete declaration has a migration row. Existing pre-program deprecations are also listed when still public.

- [ ] **Step 3: Verify code examples use canonical APIs**

Run:

```powershell
rg -n --glob '*.md' --glob '*.cs' '\.(Get|GetResource|TryGetManaged|PlayEngineMoveAsync|ApplyHumanMove)\b' src tests benchmarks samples docs
```

Expected: matches occur only in compatibility declarations, compatibility tests, and migration-guide old-API columns. Refine the search for other deprecated families listed by domain task reports.

- [ ] **Step 4: Commit after explicit authorization only**

```powershell
git add docs/migrations src/*/README.md
git commit -m "Document simplified API migrations"
```

Expected: migration documentation only. Skip unless current user authorization permits staging and committing.

---

### Task 4: Capture before-and-after complexity evidence

**Files:**
- Create: `docs/reviews/2026-07-19-complexity-simplification-audit.md`
- Read: subsystem task reports and BenchmarkDotNet result files generated during this program.

**Interfaces:**
- Consumes: current repository metrics, reviewed diffs, tests, builds, public API checks, and benchmark reports.
- Produces: the evidence used for the final 0–10 score.

- [ ] **Step 1: Capture structural metrics**

Run:

```powershell
$sourceFiles = Get-ChildItem src -Recurse -File -Filter *.cs
$sourceLines = ($sourceFiles | Get-Content | Measure-Object -Line).Lines
$testFiles = Get-ChildItem tests -Recurse -File -Filter *.cs
$testLines = ($testFiles | Get-Content | Measure-Object -Line).Lines
$obsoleteCount = (rg -n --glob '*.cs' '\[Obsolete\(' src | Measure-Object).Count
"source_files=$($sourceFiles.Count) source_lines=$sourceLines test_files=$($testFiles.Count) test_lines=$testLines obsolete_declarations=$obsoleteCount"
```

Expected: record the values as evidence, not as an arbitrary pass/fail threshold. Explain increases caused by compatibility shims and behavior-focused tests.

- [ ] **Step 2: Record high-value structural reductions**

Document verified before/after facts for:

```text
project dependency edges
expression-cache types and global state
pool acquisition/discard ownership paths
invalid public data-state constructors
ECS materialization passes and duplicated executor shells
chess position parses per batch
duplicated clock/adjudication kernels
nested Task.Run operations per stderr line
mutable ambient logging stacks
exact public API aliases without deprecation guidance
```

Expected: every claim links to a task report, diff, test, build, or benchmark result. Do not infer performance improvements from line counts.

- [ ] **Step 3: Score the five rubric dimensions**

Use this exact table:

```markdown
| Dimension | Weight | Score | Evidence |
| --- | ---: | ---: | --- |
| Cognitive simplicity | 20% | 0.0–10.0 | |
| Structural simplicity | 20% | 0.0–10.0 | |
| API simplicity | 20% | 0.0–10.0 | |
| Efficiency | 20% | 0.0–10.0 | |
| Verification quality | 20% | 0.0–10.0 | |
```

Compute the final score as the arithmetic mean. A 10/10 is permitted only when the final review finds no remaining high-confidence simplification whose benefit exceeds its compatibility, performance, or abstraction cost.

- [ ] **Step 4: Self-review evidence wording**

Search the audit for `should`, `probably`, `seems`, `TBD`, and `TODO`. Replace unsupported certainty with evidence or label it as an inference or deferred opportunity.

---

### Task 5: Run the final repository gates

**Files:**
- Verify: `bezoro.framework.sln`
- Verify: `api/PublicTypes.*.txt`
- Verify: all changed source, tests, benchmarks, samples, READMEs, plans, specs, migration, and audit artifacts.

**Interfaces:**
- Consumes: all completed subsystem and integration tasks.
- Produces: fresh evidence that the complete objective is satisfied.

- [ ] **Step 1: Inspect the complete worktree diff**

Run:

```powershell
git status --short
git diff --stat
git diff --check
```

Expected: only in-scope files are changed, the diff is coherent, and whitespace validation is clean.

- [ ] **Step 2: Verify public API baselines**

Run:

```powershell
./scripts/Export-PublicApi.ps1 -Check
```

Expected: pass. If intentional new public types are missing, regenerate baselines with the script's documented update mode, inspect the diff, and rerun `-Check`.

- [ ] **Step 3: Run the full test suite**

Run:

```powershell
dotnet test bezoro.framework.sln --no-restore --verbosity minimal
```

Expected: every test project passes with zero failures and zero warnings.

- [ ] **Step 4: Build every declared target**

Run:

```powershell
dotnet build bezoro.framework.sln --no-restore --verbosity minimal
dotnet build bezoro.framework.sln -c Release --no-restore --verbosity minimal
```

Expected: both builds pass with zero warnings and zero errors; multi-targeted source projects compile for `net9.0` and `netstandard2.1`, and SourceGen compiles for `netstandard2.0`.

- [ ] **Step 5: Run structural policy scans**

Run:

```powershell
rg -n --glob '*.cs' '#pragma warning disable CS1591|<NoWarn>.*CS1591' src
rg -n --glob '*.{cs,md}' '\b(TODO|HACK|FIXME|XXX)\b' src tests benchmarks samples docs
Get-ChildItem src,tests -Directory | ForEach-Object { if (-not (Test-Path (Join-Path $_.FullName 'README.md'))) { $_.FullName } }
```

Expected: no public-documentation suppressions, no stale code-smell marker introduced by this program, and every source/test project retains a README. Review actionable pre-existing markers against the approved constraint instead of deleting potentially useful code.

- [ ] **Step 6: Verify benchmark evidence**

Read every before/after BenchmarkDotNet report named in subsystem task reports. Confirm release-mode execution, representative parameters, and no material unexplained regression.

Expected: the final audit records exact means and allocations or explicitly labels a performance change unmeasured and therefore not eligible for an improvement claim.

- [ ] **Step 7: Run the final whole-branch review**

Use the requesting-code-review skill with a review package covering the merge base through the current worktree/authorized commits. Resolve every Critical and Important finding through the required fix-and-re-review loop.

Expected: spec compliance approved, code quality approved, and no unresolved material finding.

- [ ] **Step 8: Complete the final requirement audit**

Compare every numbered compatibility rule, workstream requirement, testing strategy item, validation gate, and delivery boundary in `docs/superpowers/specs/2026-07-19-repository-complexity-simplification-design.md` with current evidence.

Expected: every item is proven, explicitly deferred by the approved design, or identified as incomplete. Do not mark the goal complete while any required item lacks direct evidence.

- [ ] **Step 9: Commit after explicit authorization only**

```powershell
git add docs/reviews/2026-07-19-complexity-simplification-audit.md docs/superpowers
git commit -m "Record complexity simplification evidence"
```

Expected: final evidence artifacts only. Skip unless current user authorization permits staging and committing.
