# Bezoro.Logging.Tests

Characterization tests for the public `Bezoro.Logging` behavior that must remain stable while the implementation is simplified.

## Isolation

`Logger` settings and `Logger.OnLog` are process-global. Test parallelization is disabled, each test restores all mutable public settings through `LoggerSettingsScope`, and every event subscription is removed in a `finally` block.

## Coverage

- Public payload content, formatting, styles, caller information, and file metadata
- Formattable-string collection formatting
- Enabled, minimum-level, and muted-category filtering
- Exception, inner-exception, stack-trace, custom-message, and caller details
- Initial, changed, and repeated stage dispatch ordering
- Async-context nested ordering and restoration, sibling-branch isolation, and idempotent scope disposal

The allocation benchmark project records both the ordinary and nested async-context paths.

## Run

```powershell
dotnet test tests/Bezoro.Logging.Tests/Bezoro.Logging.Tests.csproj --no-restore --verbosity minimal
```
