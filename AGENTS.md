# AGENTS.md

## Core Philosophy

- **TDD**: Red → Green → Refactor. Tests before implementation.

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

## Workflow

> Refer to the global CLAUDE.md/Agents.md under *Plan Before You Execute*.

### Execution

1. **Test (= API Design)**: Tests are the primary design tool. Before writing any implementation, write tests that express the **ideal consumer experience** — the way a caller *should* interact with the API. Iterate on naming, signatures, overloads, return types, and error handling *in the test* until the usage reads naturally and feels ergonomic. Only once the test captures the desired public surface, run to confirm it fails.
2. **Implement**: Minimal code to pass the test-defined API. Run tests. Verify no fake greens. Keep it simple — performance optimization belongs in step 3.
3. **Refactor**: Optimize after green. Apply performance techniques (Span, stackalloc, pooling, etc.) here. Run tests again. For measurable performance claims, add a BenchmarkDotNet benchmark before and after optimization.
4. **Document**: Check if XML docs need updates for changed/added APIs. Check if the project's README needs updating — every project must have one and it must stay current.
5. **Verify**: Build the full solution (`dotnet build bezoro.framework.sln`) to ensure no breaks.

### Error Recovery

- If `dotnet build` or `dotnet test` fails, **diagnose the root cause, fix it, and re-run**. Do not skip or ignore failures.
- Repeat until green. If stuck after reasonable attempts, ask the user.

## Project Structure

```
bezoro.framework.sln
├── src/
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

### Build

```bash
# Full solution — use after any change to verify nothing is broken
dotnet build bezoro.framework.sln

# Release build — required before running benchmarks
dotnet build bezoro.framework.sln -c Release
```

### Test

```bash
# Full test suite
dotnet test bezoro.framework.sln

# Single project
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj

# Filter by class or method name
dotnet test --filter "FullyQualifiedName~ClassName"
dotnet test --filter "FullyQualifiedName~MethodName"

# Skip rebuild when already built (faster iteration)
dotnet test bezoro.framework.sln --no-build
```

### Benchmarks

BenchmarkDotNet **requires Release mode** — Debug builds produce meaningless results.

```bash
# Run all benchmarks in a project
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj

# Run a specific benchmark class
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj -- --filter "*ClassName*"

# Run a specific benchmark method
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj -- --filter "*MethodName*"

# Core benchmarks
dotnet run -c Release --project benchmarks/Bezoro.Core.Benchmarks/Bezoro.Core.Benchmarks.csproj
```

> **Do not set or use `DOTNET_CLI_HOME`.** This environment variable is not needed and must not be passed to any `dotnet` invocation or shell environment.

## Build Configuration

.NET 9.0 + .NET Standard 2.1 (Unity) | Warnings as errors | Nullable enabled | XML docs generated

### Dual-Targeting

The solution targets both `net9.0` and `netstandard2.1`. Runtime APIs that are net9.0-only (e.g., `FrozenDictionary`, `FrozenSet`, `SearchValues<T>`) require polyfills in the `Compatibility/` folder when used in netstandard2.1-targeted projects. C# language features (collection expressions, primary constructors, pattern matching, etc.) are compiler transforms and work on any target. Always verify that code compiles against both targets.

## Coding Standards

### Naming

| Convention     | Usage                                         |
|----------------|-----------------------------------------------|
| `PascalCase`   | Types, methods, properties, events, constants |
| `camelCase`    | Parameters, locals                            |
| `_camelCase`   | Private fields                                |
| `I` prefix     | Interfaces                                    |
| `Async` suffix | Async methods                                 |
| `Try` prefix   | `bool` + `out` pattern                        |

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

| Tag             | When                      |
|-----------------|---------------------------|
| `<summary>`     | Always                    |
| `<param>`       | Every parameter           |
| `<typeparam>`   | Every generic param       |
| `<returns>`     | Non-void methods          |
| `<exception>`   | Every thrown exception    |
| `<remarks>`     | Thread safety, perf notes |
| `<inheritdoc/>` | Interface implementations |

### Code Comments

- Comment **why**, not what
- `// TODO(user): description`
- `// HACK: reason + issue link`
- No commented-out code
- **Add `// TODO:` comments** where appropriate to flag future improvements, optimizations, or known limitations discovered during implementation. Keep them actionable and specific.

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
