# Coding Workflow Conformance Design

**Date:** 2026-07-18

**Initial score:** 8.5/10

## Goal

Bring Bezoro.Framework to a defensible 10/10 against the repository's `coding-workflow` standards while preserving public APIs, target-framework compatibility, and performance-sensitive ECS specialization. Preserve runtime behavior except where verification reproduces a concrete defect that must be fixed test-first.

## Evidence Behind The Initial Score

The repository begins from a strong baseline: 1,776 tests pass, the full solution builds for `net9.0` and `netstandard2.1` with zero warnings, the public API baseline is clean, and CI validates release builds and tests. The remaining deductions are objective repository-rule violations and demonstrated clarity gaps rather than failing behavior.

The audit found:

- twelve source files containing multiple top-level types despite the one-type-per-file rule;
- one required test-project README missing and several materially incomplete source-project READMEs;
- four source projects suppressing CS1591, masking public documentation debt across Chess UCI, ECS, GameSystems, and five source-generator entry points;
- a private `EnsureBatchBuffers(bool)` helper whose boolean selects allocation behavior;
- stale code-smell TODOs that misclassify an already-delegating façade and intentionally specialized ECS hot-path loops;
- a repository-wide `dotnet format` mismatch affecting 691 files, which is not an established project gate and would require unrelated churn.

## Chosen Approach

Use focused conformance remediation. Make only behavior-preserving organizational, documentation, and configuration changes that directly close audited gaps.

The implementation will:

1. Move every additional top-level type found by the audit into its own file without changing its namespace, accessibility, name, generic constraints, or members.
2. Replace `EnsureBatchBuffers(bool)` with explicit entity/marker and payload-index buffer helpers.
3. Replace the `World` code-smell TODO with a design rationale describing its façade role and existing delegation boundaries.
4. Preserve the arity-specific `QueryChunkWalker` implementations and explain that their duplication is intentional for generic specialization in a benchmarked hot path.
5. Remove CS1591 suppressions from the affected project files and add meaningful XML documentation for every newly exposed diagnostic, including applicable parameter, return, and exception tags.
6. Add the missing `Bezoro.GameSystems.Tests` README and complete source-project READMEs whose required purpose, key types, quick start, API reference, feature notes, or design notes are absent.
7. If targeted verification exposes a reproducible runtime defect, isolate it with a deterministic failing test before applying the smallest reliability fix.

## Compatibility And Performance

No public symbol, signature, namespace, assembly, default, exception contract, or serialized shape will change. Moving declarations between source files does not change their compiled identity. The public API export check must remain byte-for-byte clean.

Targeted verification reproduced an intermittent StartSearch/StopSearch protocol race in `UciPonderRuntime`. The sole intentional runtime change serializes those lifecycle operations so a `stop`/`isready` exchange cannot interleave with the atomic `position`/`go` start sequence. A deterministic protocol-level regression test defines that ordering contract.

The query walker loops will not be collapsed into a shared abstraction without representative benchmark evidence. Their repetition enables arity-specific generic code and avoids adding delegates, interface dispatch, collections, or per-entity branching to the ECS hot path.

The repository-wide formatter findings are excluded because formatting is not a declared verification command, existing files follow Rider-oriented conventions, and changing 691 files would violate the workflow requirement to avoid unrelated cleanup. Files touched by this work will preserve their local formatting.

## Testing And Validation

The structural changes rely on existing contract and behavior coverage. The reproduced UCI race requires a deterministic red/green regression test because it changes faulty concurrent behavior. Validation proceeds from narrow to broad:

1. Build the directly affected projects with CS1591 unsuppressed and verify zero missing-documentation diagnostics.
2. Run the UCI concurrency regression and existing integration test repeatedly, then run tests for Core, ECS, GameSystems, TypingSystem, and Chess UCI.
3. Run `scripts/Export-PublicApi.ps1 -Check`.
4. Run `dotnet test bezoro.framework.sln --verbosity minimal`.
5. Run `dotnet build bezoro.framework.sln`.
6. Inspect the complete diff and rerun structural searches for multi-type files, code-smell TODOs, README coverage, and CS1591 suppression.

## 10/10 Exit Criteria

- All audited objective rule violations are resolved.
- No public API changes; the only runtime change is the test-defined correction of concurrent UCI lifecycle ordering.
- All targeted and full tests pass.
- The full dual-target solution build has zero warnings and zero errors.
- The public API baseline check passes.
- The final diff contains no unrelated formatting or user-owned changes.
- Remaining deliberate specialization or optimization opportunities are documented as evidence-based decisions, not mislabeled defects.
