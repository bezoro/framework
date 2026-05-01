# Bezoro.Build.Tests

Integration tests for repository build and packaging scripts.

## Scope

| Area                      | Description                                                                 |
|---------------------------|-----------------------------------------------------------------------------|
| `PackageVersionScriptTests` | Exercises package version resolution from SemVer Git tags and commit height. |

## Quick Start

```bash
dotnet test tests/Bezoro.Build.Tests/Bezoro.Build.Tests.csproj
```

## Design Notes

- Tests create temporary Git repositories so version behavior is isolated from the working tree.
- The package version resolver is invoked through PowerShell to cover the same entry point used by local packaging and CI.
