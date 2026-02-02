# Repository Guidelines

## Project Structure & Module Organization

- `src/` contains the production projects, organized by module: `Bezoro.Core`, `Bezoro.ECS`, `Bezoro.Events`, `Bezoro.GameSystems`, `Bezoro.Logging`, `Bezoro.TypingSystem`, and `Bezoro.UCI`.
- `tests/` mirrors `src/` with `*.Tests` projects (e.g., `tests/Bezoro.Core.Tests`).
- `benchmarks/` hosts BenchmarkDotNet projects.
- `samples/` contains example usage and demos.
- The solution entry point is `bezoro.framework.sln`.

## Build, Test, and Development Commands

- `dotnet build bezoro.framework.sln` — build the entire solution.
- `dotnet test bezoro.framework.sln` — run all test projects.
- `dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj` — run one test project.
- `dotnet test --filter "FullyQualifiedName~ArrayExtensionsCheckTests"` — run a test class by name.
- `dotnet test --filter "FullyQualifiedName~AreEqual_WhenEqualParameters_ShouldReturnTrue"` — run a single test method.
- `dotnet run --project benchmarks/Bezoro.Core.Benchmarks/Bezoro.Core.Benchmarks.csproj -c Release` — run benchmarks.

## Coding Style & Naming Conventions

- C# uses file-scoped namespaces (e.g., `namespace Bezoro.Core.Types;`).
- Prefer `readonly`, `init`, and `record` types where appropriate; keep methods under ~40 lines.
- Nullable reference types are enabled and warnings are treated as errors.
- Performance hot paths should avoid LINQ and use `Span<T>`, `Memory<T>`, or `ArrayPool`.
- Tests follow `Method_WhenCondition_ShouldExpectation` naming.

## Testing Guidelines

- Test stack: xUnit + FluentAssertions; NSubstitute is used for external I/O only.
- Use `[Fact]` for unit tests and arrange/act/assert style.
- Coverage is collected via `coverlet.collector` (use standard `dotnet test` collectors as needed).

## Commit & Pull Request Guidelines

- Prefer Conventional Commits: `type(scope): message`, with `!` for breaking changes.
- PRs should include a concise description, linked issues (if any), and test evidence (commands or output snippets). Add before/after notes for API changes.

## Configuration & Architecture Notes

- Targets include .NET 9.0 and .NET Standard 2.1 (Unity compatibility).
- `src/Directory.Build.targets` copies `netstandard2.1` outputs to a local Unity plugin directory; update the path if your Unity location differs.
- Additional contributor guidance lives in `CLAUDE.md`.
