# AGENTS.md

## Core Philosophy

- Require TDD for behavior changes and public API changes: red, green, refactor.
- Design public APIs in tests first from the consumer perspective. Refine naming, signatures, overloads, return types, and error handling in the test before implementing.
- Favor ergonomic public surface over exposing internal complexity.
- Prefer data-oriented and ECS-style designs over traditional OOP where both are viable.
- Favor batch processing, cache-friendly layouts, low allocation pressure, immutability, and lock-free designs where practical.
- Keep the framework engine-agnostic. Worker threads should produce immutable results, and callback dispatch should use a configurable context.

## Workflow Overrides

- For behavior or public API changes, write or update the consumer-facing test first and confirm it fails before implementing.
- Implement the smallest change that satisfies the test-defined API, then refactor for performance or structure after the tests are green.
- Use targeted verification during iteration. Before completing a code change, run the most relevant tests and `dotnet build bezoro.framework.sln`.
- If `dotnet build` or `dotnet test` fails, diagnose the root cause, fix it, and rerun. Do not leave known failures behind.
- When public API, types, or observable behavior changes, review the affected XML docs and project `README.md` files before finishing.

## Repository Shape

```text
bezoro.framework.sln
├── src/
├── tests/
├── benchmarks/
└── samples/
```

- Keep namespaces aligned with folders.
- Keep one type per file.
- Avoid circular dependencies.

## Reference Commands

- Build the full solution with `dotnet build bezoro.framework.sln`.
- Build release artifacts with `dotnet build bezoro.framework.sln -c Release`.
- Run the full test suite with `dotnet test bezoro.framework.sln`.
- Run a single test project with `dotnet test tests/<Project>.Tests/<Project>.Tests.csproj`.
- Filter tests with `dotnet test --filter "FullyQualifiedName~<Name>"`.
- Run benchmarks in release mode with `dotnet run -c Release --project benchmarks/<Project>.Benchmarks/<Project>.Benchmarks.csproj`.
- Treat debug BenchmarkDotNet runs as invalid.

### Source Folders

- `Abstractions/`: public interfaces and abstract base classes
- `Attributes/`: custom attributes
- `Compatibility/`: polyfills and target-specific shims
- `DependencyInjection/`: `IServiceCollection` extensions
- `Exceptions/`: custom exception types
- `Extensions/`: extension methods grouped by target type
- `Internal/`: non-public implementation
- `Options/`: configuration POCOs
- `Primitives/`: value types, structs, and enums
- `Types/`: domain types, records, and DTOs

## .NET-Specific Project Rules

- Target `net9.0` and `netstandard2.1`. Verify both targets still compile after code changes.
- When a runtime API is `net9.0`-only, add or update the required compatibility implementation under `Compatibility/` for `netstandard2.1`.
- Treat nullable warnings as errors.
- Generate XML docs for public APIs.

### Repository Conventions

- Use file-scoped namespaces.
- Use `var` when the type is obvious from the right-hand side.
- Prefer primary constructors for DI.
- Prefer collection expressions, target-typed `new()`, and pattern matching when they improve clarity without obscuring behavior.

## Documentation

- Public APIs require XML documentation with the applicable tags for parameters, type parameters, return values, and thrown exceptions.
- Every project under `src/` and `tests/` must have a current `README.md`.
- Keep project READMEs current when public API, types, or behavior changes.
- Project READMEs should cover the project purpose, key types, quick start usage, API reference, feature-specific notes, and design notes.
- Comment why, not what.
- Keep `TODO` and `HACK` comments actionable and specific.

## Testing

- Use xUnit, FluentAssertions, and NSubstitute only at I/O boundaries.
- Mirror source structure under `tests/`.
- Name test classes `{TypeName}Tests`.
- Name tests `Method_WhenCondition_ShouldExpectation`.
- Keep tests isolated.
- Use `[Theory]` for input matrices and `[MemberData]` for more complex data.
- Use `IClassFixture<T>` only for expensive shared setup.
- Mark non-unit tests with `[Trait("Category", "Integration")]`.

## Agent Notes

> This section is maintained by the agent. Add entries as actionable project-specific patterns, pitfalls, and preferences discovered while working in this repository.

### Format

```text
### <Short Title>
Context: When or where this applies.
Rule: The concrete do, do not, prefer, or avoid guidance.
Reason: Why this matters here.
```

<!-- Agent: add learned entries below this line -->

### Package Version Source
Context: Unity package staging and CI release builds.
Rule: Resolve package versions with `scripts/Get-PackageVersion.ps1` from SemVer Git tags; use `BEZORO_PACKAGE_VERSION` or `-PackageVersion` only for explicit local overrides.
Reason: `unity-package/package.json`, release assembly metadata, and CI publishing should stay on one version source.

### Scheduler Parallelism Tests
Context: ECS tests that assert permitted parallel execution through a concurrency probe.
Rule: Give parallel-allowed assertions enough time for GitHub Actions worker threads to start; keep short waits only for tests that expect serialized execution.
Reason: Hosted Windows runners can delay ThreadPool worker startup long enough for a short probe gate to report sequential execution.
