# AGENTS.md

## Core Philosophy

- **Correctness First**: Must compile, pass tests, handle edge cases.
- **Minimal & Atomic**: Touch only what's necessary. No scope creep.
- **TDD**: Red → Green → Refactor. Tests before implementation.
- **Safety**: Never leak secrets/PII. Validate inputs.

### Public API

- **Ergonomic API is King**: Logical, easy to use, straightforward, powerful, optionally configurable.
- Favor straightforward, readable surface over exposing internal complexity.
- **Design APIs in tests first**: The test file is the design surface. Write consumer-perspective usage code *before* any interfaces or classes exist. Refine until the usage feels natural — then let the implementation follow.

### Internal Implementation

- **DoD/ECS over OOP**: Prefer data-oriented design and ECS patterns over traditional OOP.
- **Performance**: Favor batch processing, multithreading, and cache-friendly access patterns.
- **Memory Efficiency**: Prefer no-alloc, small types, and avoid GC pressure.
- **Thread Safety**: Favor immutability and lock-free designs.
- **Threading Model**: Worker threads produce immutable results. Callbacks dispatch via configurable context. Framework remains engine-agnostic.

## Codebase Scanning

- **Never shell out for file operations.** Do not use `PowerShell`, `cmd`, `cat`, `head`, `tail`, `Get-Content`, `Select-String`, `find`, or `grep` via shell to read, search, or list files. These are slow and wasteful. Always use the dedicated tools instead:
  - **Read files** → `Read` tool
  - **Search file contents** → `Grep` tool (uses ripgrep internally)
  - **Find files by name/pattern** → `Glob` tool
  - **Run shell commands** → `Bash` tool, but **only** for build/test/git operations (e.g., `dotnet build`, `dotnet test`, `git status`)
- **Exclude `bin/`, `obj/`, and `Debug/` directories** when searching or exploring the codebase unless they are directly relevant to the task (e.g., diagnosing build output or binary issues).
- These folders contain generated artifacts and add noise to search results.

## Workflow

### 1. Analysis

- Restate problem to confirm understanding.
- Identify: pros/cons, perf/memory, thread safety, risks, edge cases, constraints.
- **Stop & Ask** if requirements are ambiguous.
- Push back on suboptimal ideas; propose alternatives.

### 2. Execution

1. **Test (= API Design)**: Tests are the primary design tool. Before writing any implementation, write tests that express the **ideal consumer experience** — the way a caller *should* interact with the API. Iterate on naming, signatures, overloads, return types, and error handling *in the test* until the usage reads naturally and feels ergonomic. Only once the test captures the desired public surface, run to confirm it fails.
2. **Implement**: Minimal code to pass the test-defined API. Run tests. Verify no fake greens. Keep it simple — performance optimization belongs in step 3.
3. **Refactor**: Optimize after green. Apply performance techniques (Span, stackalloc, pooling, etc.) here. Run tests again. For measurable performance claims, add a BenchmarkDotNet benchmark before and after optimization.
4. **Document**: Check if XML docs need updates for changed/added APIs. Check if the project's README needs updating — every project must have one and it must stay current.
5. **Verify**: Build the full solution (`dotnet build bezoro.framework.sln`) to ensure no breaks.

### 3. Error Recovery

- If `dotnet build` or `dotnet test` fails, **diagnose the root cause, fix it, and re-run**. Do not skip or ignore failures.
- Repeat until green. If stuck after reasonable attempts, ask the user.

## Project Structure

```
bezoro.framework.sln
├── src/
│   ├── Bezoro.Core/        # Foundation (no dependencies)
│   ├── Bezoro.Logging/     # → Core
│   ├── Bezoro.ECS/         # → Core
│   ├── Bezoro.GameSystems/ # → Core + ECS
│   ├── Bezoro.TypingSystem/# → Core
│   └── Bezoro.UCI/         # → Core + Logging
├── tests/                  # Mirrors src/ with *.Tests projects
├── benchmarks/             # BenchmarkDotNet projects for perf-critical code
└── samples/                # Standalone usage examples (self-documenting)
```

### Source Organization

```
Abstractions/       # Public interfaces, abstract base classes
Attributes/         # Custom attributes
Compatibility/      # Polyfills for older targets
DependencyInjection/# IServiceCollection extensions
Exceptions/         # Custom exceptions
Extensions/         # Extension methods by type
Internal/           # Non-public implementation
Options/            # IOptions<T> config POCOs
Primitives/         # Value types, structs, enums
Types/              # Domain types, records, DTOs
```

Namespace mirrors folder. One type per file. No circular dependencies.

## Build Commands

```bash
dotnet build bezoro.framework.sln
dotnet test bezoro.framework.sln
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj
dotnet test --filter "FullyQualifiedName~ClassName"
dotnet test --filter "FullyQualifiedName~MethodName"
```

## Build Configuration

.NET 9.0 + .NET Standard 2.1 (Unity) | Warnings as errors | Nullable enabled | XML docs generated

### Dual-Targeting

The solution targets both `net9.0` and `netstandard2.1`. Runtime APIs that are net9.0-only (e.g., `FrozenDictionary`, `FrozenSet`, `SearchValues<T>`) require polyfills in the `Compatibility/` folder when used in netstandard2.1-targeted projects. C# language features (collection expressions, primary constructors, pattern matching, etc.) are compiler transforms and work on any target. Always verify that code compiles against both targets.

## Coding Standards

### Naming

| Convention | Usage |
|------------|-------|
| `PascalCase` | Types, methods, properties, events, constants |
| `camelCase` | Parameters, locals |
| `_camelCase` | Private fields |
| `I` prefix | Interfaces |
| `Async` suffix | Async methods |
| `Try` prefix | `bool` + `out` pattern |

### Style

- File-scoped namespaces, one type per file
- `var` when type obvious from RHS
- Primary constructors for DI
- Collection expressions: `[1, 2, 3]`
- Target-typed `new()`
- Pattern matching over `is` + cast
- Prefer `readonly`, `required`, `init`, `record`

### Nullability

- Nullable refs enabled; warnings as errors
- Guards: `ArgumentNullException.ThrowIfNull(param)`
- Prefer `??` and `?.` over explicit checks
- Use `[NotNullWhen]`, `[MaybeNullWhen]` for analyzers

### Async

- `Task`/`Task<T>` default; `ValueTask` only for hot paths with sync completion
- Always propagate `CancellationToken`
- `ConfigureAwait(false)` in library code
- No `async void` (except event handlers)
- No `.Result`/`.Wait()`

### Error Handling

- Exceptions for unrecoverable cases only
- Result types (`Try<T>`) for expected failures
- Exception filters: `catch (Ex e) when (condition)`

### Performance

- Hot paths: `Span<T>`, `Memory<T>`, `ArrayPool<T>.Shared`
- `FrozenDictionary`/`FrozenSet` for read-heavy collections
- `SearchValues<T>` for repeated `IndexOfAny`
- Avoid LINQ in tight loops
- `static` lambdas to avoid closures
- `struct` for small, short-lived types
- `stackalloc` for small fixed buffers
- `ObjectPool<T>` for reusable objects

## Documentation

### XML Docs (Required for Public API)

```csharp
/// <summary>Brief description.</summary>
/// <typeparam name="T">Type param description.</typeparam>
/// <param name="input">Param description with constraints.</param>
/// <returns>What it returns.</returns>
/// <exception cref="Ex">When thrown.</exception>
```

| Tag | When |
|-----|------|
| `<summary>` | Always |
| `<param>` | Every parameter |
| `<typeparam>` | Every generic param |
| `<returns>` | Non-void methods |
| `<exception>` | Every thrown exception |
| `<remarks>` | Thread safety, perf notes |
| `<inheritdoc/>` | Interface implementations |

### Code Comments

- Comment **why**, not what
- `// TODO(user): description`
- `// HACK: reason + issue link`
- No commented-out code

### README (Required per Project)

Every project under `src/` and `tests/` must have a `README.md`. After any change to public API, types, or behavior, check if the project's README needs updating. Stale READMEs are a bug.

```
# {Project}          → One-sentence description
## Types             → Table: Type | Description
## Quick Start       → 3-10 line example
## API Reference     → Table per interface: Member | Description
## {Feature}         → Feature-specific docs + examples
## Design Notes      → Key decisions, thread safety, perf, edge cases
```

## Testing

**Stack**: xUnit + FluentAssertions + NSubstitute (I/O only)

**Naming**: `Method_WhenCondition_ShouldExpectation`

```csharp
namespace Bezoro.Core.Tests;

[TestSubject(typeof(Target))]
public class TargetTests
{
    [Fact]
    public void Method_WhenCondition_ShouldExpectation()
    {
        // Arrange
        var sut = new Target();
        // Act
        var result = sut.Method();
        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("in1", "out1")]
    [InlineData("in2", "out2")]
    public void Method_WhenInputs_ShouldReturn(string input, string expected) =>
        new Target().Method(input).Should().Be(expected);
}
```

### Test Project Organization

Test projects mirror the source project folder structure. For example:

```
tests/Bezoro.ECS.Tests/
├── ArchetypeTests.cs       # mirrors src/Bezoro.ECS/Types/Archetype.cs
├── QueryTests.cs           # mirrors src/Bezoro.ECS/Types/Query.cs
├── CommandBufferTests.cs   # mirrors src/Bezoro.ECS/Types/CommandBuffer.cs
└── Fixtures/               # Shared test fixtures (IClassFixture<T>)
```

One test class per source type. Test class name = `{TypeName}Tests`. Place shared fixtures in a `Fixtures/` folder.

**Guidelines**: Isolated tests | One behavior per test | `[Theory]` for multiple inputs | `[MemberData]` for complex data | `IClassFixture<T>` for expensive setup | Mock only I/O boundaries | `[Trait("Category", "Integration")]` for non-unit tests

## Git Branching

- **Never commit directly to `main`**. The `main` branch is protected.
- Work on `develop` or create feature branches (`feat/<name>`, `fix/<name>`, etc.) as appropriate.
- Create feature branches when the change is non-trivial or spans multiple commits.
- PRs target `main` from `develop` or feature branches.
- PR titles follow commit format: `<type>(<scope>): <description>`. Body summarizes changes with bulleted list.

## Commits

**Format**: `<type>(<scope>): <description>`

**Types**: `feat` | `fix` | `build` | `chore` | `ci` | `docs` | `perf` | `refactor` | `revert` | `style` | `test`

**Breaking**: `feat(api)!: message` or footer `BREAKING CHANGE: description`

**Body**: Use bulleted lists (`-`) to describe individual changes.

**SemVer**: `feat` → MINOR | `fix` → PATCH | `!`/`BREAKING CHANGE` → MAJOR

### Commit Workflow

1. **Check state**: Run `git status` and `git diff` to review all changes.
2. **Stage selectively**: Only `git add` files related to the current task. Never use `git add -A` or `git add .` blindly.
3. **Review staged**: Run `git diff --staged` to verify only intended changes are staged.
4. **Commit**: Create commit with appropriate message format.

**Never commit unrelated changes**. If unrelated modifications exist, leave them unstaged or ask the user how to proceed.
