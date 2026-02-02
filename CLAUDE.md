# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build entire solution
dotnet build bezoro.framework.sln

# Run all tests
dotnet test bezoro.framework.sln

# Run tests for a specific project
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj

# Run a specific test class
dotnet test --filter "FullyQualifiedName~ArrayExtensionsCheckTests"

# Run a specific test method
dotnet test --filter "FullyQualifiedName~AreEqual_WhenEqualParameters_ShouldReturnTrue"

# Run benchmarks
dotnet run --project benchmarks/Bezoro.Core.Benchmarks/Bezoro.Core.Benchmarks.csproj -c Release
```

## Architecture Overview

Bezoro.Framework is a multi-project .NET solution targeting both .NET 9.0 and .NET Standard 2.1 (for Unity compatibility).

### Project Dependencies

```
Bezoro.Core (foundation - no dependencies)
    ↑
    ├── Bezoro.Logging
    ├── Bezoro.ECS
    ├── Bezoro.GameSystems
    ├── Bezoro.TypingSystem
    └── Bezoro.Chess
            ↓
        Bezoro.UCI (depends on Core + Logging)
```

### Key Projects

- **Bezoro.Core**: Foundation library with utilities, extension methods, and primitives (`Try`, `Singleton`, `ResultFactory`, `Grid2D`, `SwapbackArray`)
- **Bezoro.ECS**: Lightweight Entity Component System - `World` coordinates entities, components (`IComponent` structs), and systems (`ISystem`)
- **Bezoro.UCI**: UCI protocol client for chess engines (Stockfish) - process-based transport with async operations
- **Bezoro.Chess**: Chess engine with move generation per piece type and FEN parsing

### Source Organization (Bezoro.Core pattern)

```
src/Bezoro.Core/
├── Abstractions/     # Interfaces
├── Compatibility/    # Polyfills (IsExternalInit, RequiredMemberAttribute)
├── Extensions/       # Extension methods by type (ArrayExtensions, StringExtensions, etc.)
├── Helpers/          # Static helper classes
├── Types/            # Value types and exceptions
└── Utilities/        # Constants, StringTags
```

### Test Structure

```csharp
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

[TestSubject(typeof(TargetClass))]
public class TargetClassTests
{
    [Fact]
    public void MethodName_WhenCondition_ShouldExpectation()
    {
        // Arrange, Act, Assert with FluentAssertions
        result.Should().BeTrue();
    }
}
```

### Commit Messages

Conventional Commits format: `type(scope): message`
- Types: `feat`, `fix`, `refactor`, `test`, `chore`, `docs`, `style`
- Breaking changes: `refactor(core)!: description`

## Build Configuration

- **Warnings as errors**: Enabled (`TreatWarningsAsErrors=true`)
- **Nullable reference types**: Enabled
- **Code style enforcement**: Enabled in build
- **XML documentation**: Generated
