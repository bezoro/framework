# Bezoro.Core

Bezoro.Core is the foundational library of the Bezoro Framework. It provides high-performance primitives, utility types,
extensions, and small infrastructure pieces used across other modules. It targets .NET 9.0 and .NET Standard 2.1 for
Unity compatibility.

## Key Features

- Core value types: `Percent`, `Color` (RGBA parsing/formatting), `UIntVector2`, `Grid2D`/`GridSpan2D`.
- Collections and data structures: `SwapbackArray<T>` for O(1) removals and `SingleItemEnumerable<T>`.
- Result and Try helpers for safe functional-style flow without exception noise.
- Object pooling: thread-safe `ObjectPool<T>` with policies, async waiting, statistics, and scoped handles.
- Utilities: `StringTags`, `Constants`, validation helpers, and array helpers.
- Extensions: guard helpers (`ThrowIfNull`, `ThrowIfEmpty`), spans, strings, enums, numerics.
- Code generation helpers: `CodeWriter` and `CSharpCodeBuilder`.
- Compatibility shims for older targets (e.g., `CallerArgumentExpressionAttribute`, `RequiredMemberAttribute`).

## Installation

Add a reference to `Bezoro.Core` in your project:

```xml
<ProjectReference Include="path/to/Bezoro.Core.csproj" />
```

## Quick Start

### Object Pooling

```csharp
using System.Text;
using Bezoro.Core.Types;
using Bezoro.Core.Types.Pool;

var pool = new ObjectPool<StringBuilder>(() => new StringBuilder(), PoolOptions.HighThroughput);

using var handle = pool.RentHandle();
handle.Value.Append("Hello, pooled world!");

var stats = pool.Statistics;

// Choose lifecycle intent explicitly:
pool.Clear();                  // Dispose available IDisposable values.
pool.ClearWithPolicyDiscard(); // Route available values through the policy discard callback.
```

### Collections and Lifecycle APIs

```csharp
using Bezoro.Core.Types;

var entityIds = new SwapbackArray<int> { 10, 20, 30 };
if (entityIds.TryGetIndex(20, out uint index))
	Console.WriteLine($"Entity 20 is at {index}.");

entityIds.ClearRetainingCapacity(); // Reuse the allocation.
entityIds.Clear();                   // Clear and shrink to minimum capacity.

Singleton<GameServices>.ConfigureFactory(() => new GameServices());
Singleton<GameServices>.ConfigureFactoryAndRecreate(() => new GameServices());
Singleton<GameServices>.Reset();
Singleton<GameServices>.ResetAndDispose();
```

Use the named lifecycle methods to make disposal, recreation, capacity retention, and policy-discard behavior explicit at
the call site.

### String and Enum Formatting

```csharp
using Bezoro.Core.Extensions;
using Bezoro.Core.Types;

var namedColor = "warning".Color("red");
var rgbaColor = "warning".Color(new Color(1f, 0f, 0f, 1f));

var enumLabel = GameState.Running.ToString().Bold().Color("green");
```

`Color(string)` and `Color(Color)` are the primary color APIs. Format enum values through `ToString()` and then compose
the regular string extensions. `ColorHex`, parameterless named-color string methods, and enum styling wrappers remain
callable during the compatibility window with their existing output, but are obsolete and should be migrated to these
canonical forms.

### Result + Try Helpers

```csharp
using Bezoro.Core.Types;

sealed record ParseFailure(string Input) : IFailureReason;

static Result<int> ParseNumber(string input)
{
	return int.TryParse(input, out var value)
		? ResultFactory.Succeeded(value)
		: ResultFactory.Failed<int>(new ParseFailure(input));
}

var (ok, value) = Try.TryGet(() => int.Parse("42"));
var result = ParseNumber("not-a-number");
```

### Grid2D

```csharp
using Bezoro.Core.Types;

using var grid = new Grid2D<int>(width: 10, height: 10, defaultValue: 0, usePooling: true);
grid[2, 3] = 5;
```

## API Reference

| API | Purpose |
| --- | --- |
| `ObjectPool<T>` | Rents reusable objects with configurable capacity, reset, exhaustion, and async-wait policies. |
| `SwapbackArray<T>` | Stores unordered values with constant-time removal by replacing a removed slot with the final item. |
| `Singleton<T>` | Provides explicit configure, recreate, reset, and dispose lifecycle operations for a shared instance. |
| `Result<T>` / `ResultFactory` | Represents explicit success or a typed failure reason. |
| `Try` | Wraps exception-producing operations in compact success/value results. |
| `Grid2D<T>` / `GridSpan2D<T>` | Provides owned and non-owning two-dimensional storage views. |
| `Percent` / `Color` | Provides validated, format-aware value types for common framework data. |
| String formatting extensions | Compose rich-text formatting with canonical `Color(string)` and `Color(Color)` overloads. |
| `CodeWriter` / `CSharpCodeBuilder` | Builds deterministic generated source text. |

## Feature Notes

- `SwapbackArray<T>` does not preserve item order after removal.
- `TryGetIndex(T, out uint)` returns `false` with index zero when an item is absent.
- Pooling APIs expose statistics and scoped handles; dispose rented handles to return their values.
- Compatibility shims are compiled only where the target framework lacks the corresponding runtime type.
- Performance-sensitive collections and spans avoid allocations where their ownership contract permits it.

## Design Notes

- Core contains engine-independent primitives shared by the other framework packages.
- Public value types validate their invariants at construction or mutation boundaries.
- Multi-targeted implementations preserve the same public contract on .NET 9 and .NET Standard 2.1.
- Specialized data structures are preferred only where their semantics or measured performance justify them.

## Target Frameworks

- .NET 9.0
- .NET Standard 2.1 (Unity)

---
Part of the Bezoro Framework.
