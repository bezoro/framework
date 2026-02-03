# AGENTS.md

## Core Philosophy

- **Ergonomic API is King**: Logical, easy to use, straightforward, powerful, optionally configurable.
- **Correctness First**: Must compile, pass tests, handle edge cases.
- **Minimal & Atomic**: Touch only what's necessary. No scope creep.
- **TDD**: Red → Green → Refactor. Tests before implementation.
- **Safety**: Never leak secrets/PII. Validate inputs.

## Workflow

### 1. Analysis

- Restate problem to confirm understanding.
- Identify: pros/cons, perf/memory, thread safety, risks, edge cases, constraints.
- **Stop & Ask** if requirements are ambiguous.
- Push back on suboptimal ideas; propose alternatives.

### 2. Execution

1. **Test**: Write/update test. Run to confirm it fails.
2. **Implement**: Minimal code to pass. Run tests. Verify no fake greens.
3. **Refactor**: Optimize after green. Run tests again.

## Project Structure

```
bezoro.framework.sln
├── src/
│   ├── Bezoro.Core/        # Foundation (no dependencies)
│   ├── Bezoro.Logging/     # → Core
│   ├── Bezoro.ECS/         # → Core
│   ├── Bezoro.GameSystems/ # → Core
│   ├── Bezoro.TypingSystem/# → Core
│   └── Bezoro.UCI/         # → Core + Logging
├── tests/                  # Mirrors src/ with *.Tests projects
├── benchmarks/
└── samples/
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

### README Structure

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

**Guidelines**: Isolated tests | One behavior per test | `[Theory]` for multiple inputs | `[MemberData]` for complex data | `IClassFixture<T>` for expensive setup | Mock only I/O boundaries | `[Trait("Category", "Integration")]` for non-unit tests

## Commits

**Format**: `<type>(<scope>): <description>`

**Types**: `feat` | `fix` | `build` | `chore` | `ci` | `docs` | `perf` | `refactor` | `revert` | `style` | `test`

**Breaking**: `feat(api)!: message` or footer `BREAKING CHANGE: description`

**SemVer**: `feat` → MINOR | `fix` → PATCH | `!`/`BREAKING CHANGE` → MAJOR
