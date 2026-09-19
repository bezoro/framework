# Bezoro.Core.Tests

Unit tests for `Bezoro.Core` primitives, extensions, helpers, and utility types.

## Types

| Type/Area           | Description                                                                                                           |
|---------------------|-----------------------------------------------------------------------------------------------------------------------|
| `Helpers/*Tests`    | Mirrors `src/Bezoro.Core/Helpers` (converter, array, comparers, exception, and validation helper behavior).           |
| `Types/*Tests`      | Mirrors `src/Bezoro.Core/Types` (grid, result, singleton, try helpers, percent, ranges, vectors, pools, collections). |
| `Utilities/*Tests`  | Mirrors `src/Bezoro.Core/Utilities` (`StringTags` processing and replacement behavior).                               |
| `Extensions/*Tests` | Mirrors `src/Bezoro.Core/Extensions` (arrays, collections, enums, spans, memory, strings, and type extensions).       |

## Quick Start

```bash
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj
```

## Design Notes

- Tests are deterministic and focus on behavior contracts and edge cases.
- High-churn suites are split into focused files (`Types/Try*Tests`, `Extensions/StringExtensions*Tests`) for maintainability.
- Assertions use FluentAssertions for consistency and readable failures.
- Pool tests protect the owned-capacity invariant: failed creation reservations are released, and owned items rejected
  during rent validation or return reset are removed and discarded exactly once.
- `PooledObjectHandle<T>` tests cover copied handles to ensure that all struct copies share disposal state and return their
  value exactly once.
- `SwapbackArray<T>` lookup tests exercise `TryGetIndex(T, out uint)` as the canonical lookup kernel; the legacy nullable
  index helper remains covered for compatibility.
- Compatibility tests preserve obsolete expression guards, lifecycle boolean overloads, and formatting convenience shims
  while guiding callers to the canonical APIs.

## Test Conventions

- Test method names follow `Method_WhenCondition_ShouldExpectation`.
- Each test class targets a focused source type or behavior slice.
- Disposable resources in tests use explicit ownership/disposal patterns.
