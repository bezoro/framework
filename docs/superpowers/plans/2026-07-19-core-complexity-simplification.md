# Core Complexity Simplification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Core pooling, guards, collection lookup, formatting conveniences, and lifecycle operations easier to reason about while preserving public compatibility and measured hot-path behavior.

**Architecture:** `ObjectPool<T>` will own one acquire kernel, reference-identity ownership state, and one owned-discard path; copied `PooledObjectHandle<T>` values will share one internal lease object. Boolean guards and general formatting/lookup operations become canonical, while superseded helpers and convenience overloads remain forwarding `[Obsolete]` shims. Named lifecycle operations replace optional booleans without changing their old behavior.

**Tech Stack:** C# 13, .NET 9, .NET Standard 2.1, xUnit, FluentAssertions, BenchmarkDotNet, PowerShell.

## Global Constraints

- Follow red-green-refactor for every behavior or public API change; add the consumer-facing test and observe the stated failure before implementation.
- Keep `net9.0` and `netstandard2.1` compiling with nullable/analyzer warnings treated as errors.
- Preserve all current public types and members. Every superseded member must remain and use the exact canonical replacement message stated in its task.
- Preserve foreign-object returns, async waiting, policy callbacks, pool statistics, handle conversion, named color output, singleton behavior, and all dormant APIs.
- Keep namespaces aligned with folders and one top-level type per file.
- Migrate repository source, tests, benchmarks, samples, and README examples to canonical APIs; only narrowly scoped compatibility tests may suppress `CS0618`.
- Do not stage or commit unless current user authorization permits it. The commit commands below are implementation checkpoints, not authorization.

---

### Task 1: Define object-pool ownership and failure contracts in tests

**Files:**
- Create: `tests/Bezoro.Core.Tests/Types/Pool/ThrowingCreatePolicy.cs`
- Create: `tests/Bezoro.Core.Tests/Types/Pool/TrackingPoolPolicy.cs`
- Create: `tests/Bezoro.Core.Tests/Types/Pool/ObjectPoolCapacityInvariantTests.cs`
- Modify: `tests/Bezoro.Core.Tests/Types/Pool/ObjectPoolReturnTests.cs`
- Modify: `tests/Bezoro.Core.Tests/Types/Pool/ObjectPoolTrimExcessTests.cs`
- Verify: `src/Bezoro.Core/Types/Pool/ObjectPool.cs`

**Interfaces:**
- Preserve: `ObjectPool<T>.Rent`, `TryRent`, both `RentAsync` overloads, `Return`, `TrimExcess`, `Clear`, and `Dispose`.
- Establish: every owned object occupies exactly one capacity slot until the single discard path releases it; accepted foreign returns acquire one slot.

- [ ] **Step 1: Add deterministic policy test doubles**

Create one type per file with these implementations:

```csharp
internal sealed class ThrowingCreatePolicy(Exception exception) : IPoolPolicy<object>
{
	public int CreateCount { get; private set; }

	public object Create()
	{
		CreateCount++;
		throw exception;
	}

	public void OnDiscard(object item) { }
	public bool Reset(object item) => true;
	public bool Validate(object item) => true;
}

internal sealed class TrackingPoolPolicy : IPoolPolicy<object>
{
	public int CreateCount { get; private set; }
	public int DiscardCount { get; private set; }
	public int ResetCount { get; private set; }
	public int ValidateCount { get; private set; }
	public bool ResetResult { get; set; } = true;
	public bool ValidateResult { get; set; } = true;

	public object Create()
	{
		CreateCount++;
		return new();
	}

	public void OnDiscard(object item) => DiscardCount++;

	public bool Reset(object item)
	{
		ResetCount++;
		return ResetResult;
	}

	public bool Validate(object item)
	{
		ValidateCount++;
		return ValidateResult;
	}
}
```

- [ ] **Step 2: Add the creation rollback test**

Create `ObjectPoolCapacityInvariantTests.cs` with this consumer contract:

```csharp
[Fact]
public void Rent_WhenFactoryThrows_ShouldReleaseReservedCapacity()
{
	var policy = new ThrowingCreatePolicy(new InvalidOperationException("factory failed"));
	var pool = new ObjectPool<object>(policy, new() { MaxCapacity = 1 });

	Action first = () => pool.Rent();
	Action second = () => pool.Rent();

	first.Should().Throw<InvalidOperationException>().WithMessage("factory failed");
	second.Should().Throw<InvalidOperationException>().WithMessage("factory failed");
	pool.TotalCount.Should().Be(0);
	policy.CreateCount.Should().Be(2);
}
```

- [ ] **Step 3: Add exact release-once tests**

In the same file, add these facts:

```csharp
[Fact]
public void Rent_WhenAvailableItemFailsValidation_ShouldReleaseItsCapacityOnce()
{
	var policy = new TrackingPoolPolicy { ValidateResult = true };
	var pool = new ObjectPool<object>(policy, new() { MaxCapacity = 1, TrackStatistics = true });
	var item = pool.Rent();
	pool.Return(item).Should().BeTrue();
	policy.ValidateResult = false;

	pool.TryRent(out var rented).Should().BeFalse();
	rented.Should().BeNull();
	pool.TotalCount.Should().Be(0);
	pool.Statistics.TotalDiscarded.Should().Be(1);
	policy.DiscardCount.Should().Be(1);
}

[Fact]
public void Return_WhenResetRejectsOwnedItem_ShouldReleaseItsCapacityOnce()
{
	var policy = new TrackingPoolPolicy { ResetResult = false };
	var pool = new ObjectPool<object>(policy, new() { MaxCapacity = 1, TrackStatistics = true });
	var item = pool.Rent();

	pool.Return(item).Should().BeFalse();

	pool.TotalCount.Should().Be(0);
	pool.Statistics.TotalDiscarded.Should().Be(1);
	policy.DiscardCount.Should().Be(1);
}
```

The validation test uses non-creating `TryRent`, preserving the existing rule that validation applies to available pooled instances rather than freshly created instances.

- [ ] **Step 4: Pin foreign-return compatibility and capacity accounting**

Add to `ObjectPoolReturnTests.cs`:

```csharp
[Fact]
public void Return_WhenItemWasNotCreatedByPool_ShouldAcceptAndOwnAvailableCapacity()
{
	var pool = new ObjectPool<object>(() => new(), new() { MaxCapacity = 1 });
	var foreign = new object();

	pool.Return(foreign).Should().BeTrue();

	pool.TotalCount.Should().Be(1);
	pool.AvailableCount.Should().Be(1);
	pool.Rent().Should().BeSameAs(foreign);
}
```

Also add a max-capacity case that rents the pool-created item, returns a foreign object successfully into its open slot, then returns the original item and expects `false`, one available object, and `TotalCount == 1`. This locks in compatibility without allowing capacity inflation.

- [ ] **Step 5: Pin trim, clear, and dispose discard accounting**

Extend `ObjectPoolTrimExcessTests.cs` and the existing clear/dispose tests so each owned object produces exactly one release action and one `TotalCount` decrement. For `ClearWithPolicyDiscard` introduced later, assert the policy callback runs; for `Clear`, assert `IDisposable.Dispose` runs directly, preserving the two legacy `Clear(bool)` behaviors.

- [ ] **Step 6: Run the red tests**

```powershell
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj --filter "FullyQualifiedName~ObjectPoolCapacityInvariantTests|FullyQualifiedName~ObjectPoolReturnTests|FullyQualifiedName~ObjectPoolTrimExcessTests" --no-restore --verbosity minimal
```

Expected: failures show leaked `TotalCount` after factory/validation/reset rejection and `TotalCount == 0` after accepting a foreign return. No production code is changed yet.

- [ ] **Step 7: Commit after explicit authorization only**

```powershell
git add tests/Bezoro.Core.Tests/Types/Pool
git commit -m "Test object pool ownership invariants"
```

---

### Task 2: Implement one acquire kernel and one owned-discard path

**Files:**
- Create: `src/Bezoro.Core/Internal/ReferenceIdentityComparer.cs`
- Create: `src/Bezoro.Core/Types/Pool/PoolItemState.cs`
- Modify: `src/Bezoro.Core/Types/Pool/ObjectPool.cs`
- Modify: `src/Bezoro.Core/Abstractions/IPool.cs`
- Modify: `tests/Bezoro.Core.Tests/Types/Pool/ObjectPoolClearTests.cs`
- Modify: `tests/Bezoro.Core.Tests/Types/Pool/ObjectPoolDisposeTests.cs`

**Interfaces:**
- Internal state: `ConcurrentDictionary<T, PoolItemState>` constructed with `ReferenceIdentityComparer<T>.Instance`.
- Internal kernel: `TryAcquire(bool allowCreate, [NotNullWhen(true)] out T? item)`.
- Canonical lifecycle: `Clear()` and `ClearWithPolicyDiscard()`.
- Compatibility shim: `[Obsolete("Use Clear() or ClearWithPolicyDiscard() instead.")] Clear(bool disposeItems)`.

- [ ] **Step 1: Add the reference comparer and state enum**

Implement the comparer exactly as reference equality plus runtime identity hash:

```csharp
namespace Bezoro.Core.Internal;

internal sealed class ReferenceIdentityComparer<T> : IEqualityComparer<T> where T : class
{
	public static ReferenceIdentityComparer<T> Instance { get; } = new();

	private ReferenceIdentityComparer() { }

	public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
	public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}
```

`PoolItemState` has only `Available` and `Rented` values.

- [ ] **Step 2: Replace split acquisition logic**

Add `_ownedItems` and make these paths call `TryAcquire`:

```csharp
public bool TryRent([NotNullWhen(true)] out T? item) => TryAcquire(false, out item);

private T RentCore()
{
	if (TryAcquire(true, out var item)) return item;
	throw new PoolExhaustedException(typeof(T), _options.MaxCapacity);
}
```

`TryAcquire` must:

1. return `false` when disposed;
2. pop available entries until it finds one whose identity transitions atomically from `Available` to `Rented`;
3. validate each transitioned item and call `ReleaseOwnedItem(item, _policy.OnDiscard)` on rejection;
4. return `false` when `allowCreate` is false;
5. otherwise call `TryCreateOwnedItem`, validate the new item under the same rule, then notify rent and update statistics exactly once.

Both async overloads must call `TryAcquire(true, out item)` before waiting. `TryAcquire` validates identities popped from `_available`; a freshly created identity is returned without another validation call, matching current behavior. Remove `CreateNewItem`, `RentCore`'s duplicate stack loop, and direct `TryCreateNewItem` calls from public paths.

- [ ] **Step 3: Make creation reservation exception-safe**

`TryCreateOwnedItem` must reserve `_totalCount` with compare/exchange, execute `_policy.Create()` inside `try`, reject `null` with `InvalidOperationException("Pool policy returned null from Create().")`, and add the identity as `Rented`. Any exception or duplicate identity must decrement the reservation before rethrowing. Increment `TotalCreated` only after ownership registration succeeds.

`PrewarmPool` must reuse the same reservation/creation helper, transition each created item to `Available`, and push it; it must not duplicate creation or statistics logic.

- [ ] **Step 4: Centralize ownership release**

Implement one release kernel:

```csharp
private void ReleaseOwnedItem(T item, Action<T> release)
{
	if (!_ownedItems.TryRemove(item, out _)) return;

	try
	{
		release(item);
	}
	finally
	{
		Interlocked.Decrement(ref _totalCount);
		if (_options.TrackStatistics) Interlocked.Increment(ref _totalDiscarded);
	}
}
```

Validation/reset/capacity rejection and trim pass `_policy.OnDiscard`; `Clear()` and `Dispose()` pass `DisposeItem`; `ClearWithPolicyDiscard()` passes `_policy.OnDiscard`. This preserves both legacy `Clear(bool)` branches while centralizing ownership removal, count release, and discard statistics. If `release` throws, the `finally` block still restores the capacity invariant.

All validation rejection, reset rejection, policy/capacity rejection, trim, clear, dispose, and creation cleanup paths must call this single kernel. No caller may separately decrement `_totalCount`.

- [ ] **Step 5: Preserve foreign returns with atomic ownership**

`Return` must reset first, then:

- transition an owned identity from `Rented` to `Available`; or
- reserve one capacity slot and add a foreign identity directly as `Available`;
- reject an owned duplicate already marked `Available` without discarding the available instance;
- if capacity is full but a currently rented owned identity exists, atomically remove one `Rented` identity and register the foreign identity as `Available` without changing `_totalCount`; a later return of the replaced identity follows the unowned-capacity-rejection path. This preserves the legacy behavior in which a foreign return can fill an open rented slot at maximum capacity;
- if capacity reservation, replacement, or identity registration fails, call `_policy.OnDiscard` for the unowned candidate without decrementing `_totalCount`;
- push and signal only after state registration succeeds.

This is the only permitted distinction between owned and unowned discard paths.

- [ ] **Step 6: Add named clear APIs and forwarding shims**

Change `IPool<T>` and `ObjectPool<T>` to expose:

```csharp
void Clear();
void ClearWithPolicyDiscard();

[Obsolete("Use Clear() or ClearWithPolicyDiscard() instead.")]
void Clear(bool disposeItems);
```

`Clear()` uses direct `IDisposable` disposal, matching legacy `disposeItems: true`; `ClearWithPolicyDiscard()` invokes `IPoolPolicy<T>.OnDiscard`, matching legacy `disposeItems: false`; the obsolete overload chooses between those methods. Update XML docs to describe these exact behaviors.

- [ ] **Step 7: Run the focused pool suite**

```powershell
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj --filter "FullyQualifiedName~Types.Pool" --no-restore --verbosity minimal
```

Expected: all pool tests pass with zero warnings. If the test runner reports any obsolete warning, migrate non-compatibility calls to named methods and place `#pragma warning disable CS0618` only around a dedicated forwarding-shim assertion.

- [ ] **Step 8: Commit after explicit authorization only**

```powershell
git add src/Bezoro.Core/Internal src/Bezoro.Core/Types/Pool src/Bezoro.Core/Abstractions/IPool.cs tests/Bezoro.Core.Tests/Types/Pool
git commit -m "Unify object pool ownership paths"
```

---

### Task 3: Make copied pool handles share exactly-once lease state

**Files:**
- Create: `src/Bezoro.Core/Types/Pool/PooledObjectHandleState.cs`
- Modify: `src/Bezoro.Core/Types/Pool/PooledObjectHandle.cs`
- Modify: `tests/Bezoro.Core.Tests/Types/Pool/PooledObjectHandleDisposeTests.cs`
- Modify: `tests/Bezoro.Core.Tests/Types/Pool/PooledObjectHandleValueTests.cs`

**Interfaces:**
- Preserve public mutable struct `PooledObjectHandle<T>`, `Value`, `IsDisposed`, implicit conversion, and `Dispose`.
- Internal state: one reference object shared by every struct copy.

- [ ] **Step 1: Add failing copy semantics tests**

Add:

```csharp
[Fact]
public void Dispose_WhenHandleWasCopied_ShouldReturnSharedValueExactlyOnce()
{
	var pool = new ObjectPool<object>(() => new());
	var first = pool.RentHandle();
	var second = first;

	first.Dispose();
	second.Dispose();

	pool.AvailableCount.Should().Be(1);
	first.IsDisposed.Should().BeTrue();
	second.IsDisposed.Should().BeTrue();
}

[Fact]
public void Value_WhenAnotherCopyWasDisposed_ShouldThrowObjectDisposedException()
{
	var pool = new ObjectPool<object>(() => new());
	var first = pool.RentHandle();
	var second = first;
	first.Dispose();

	Action action = () => _ = second.Value;

	action.Should().Throw<ObjectDisposedException>();
}
```

- [ ] **Step 2: Run the red handle tests**

```powershell
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj --filter "FullyQualifiedName~PooledObjectHandle" --no-restore --verbosity minimal
```

Expected: the copy-dispose test reports two available entries and the copied `Value` remains accessible.

- [ ] **Step 3: Implement shared reference state**

`PooledObjectHandleState<T>` owns nullable `IPool<T>` and `T` fields. Its `Value`, `IsDisposed`, and `Dispose` implement the current semantics using `Interlocked.Exchange`; `Dispose` returns the value once. `PooledObjectHandle<T>` becomes a one-field struct:

```csharp
private readonly PooledObjectHandleState<T>? _state;

internal PooledObjectHandle(T value, IPool<T> pool) => _state = new(value, pool);

public readonly bool IsDisposed => _state is null || _state.IsDisposed;
public readonly T Value => _state?.Value ?? throw new ObjectDisposedException(nameof(PooledObjectHandle<T>));
public void Dispose() => _state?.Dispose();
```

Update the debugger display and remarks to say copies share disposal state. Do not introduce another public lease type.

- [ ] **Step 4: Run all handle and pool tests**

```powershell
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj --filter "FullyQualifiedName~PooledObjectHandle|FullyQualifiedName~ObjectPool" --no-restore --verbosity minimal
```

Expected: all tests pass; copied handles return once and every copy observes disposal.

- [ ] **Step 5: Commit after explicit authorization only**

```powershell
git add src/Bezoro.Core/Types/Pool/PooledObjectHandle*.cs tests/Bezoro.Core.Tests/Types/Pool/PooledObjectHandle*.cs
git commit -m "Share pooled handle lease state"
```

---

### Task 4: Remove global expression-guard state and deprecate broad helpers

**Files:**
- Modify: `src/Bezoro.Core/Extensions/GenericExtensions.cs`
- Modify: `src/Bezoro.Core/Helpers/ExceptionHelper.cs`
- Modify: `src/Bezoro.Core/Helpers/ValidationHelper.cs`
- Modify: `src/Bezoro.Chess.UCI/API/Common/Extensions/CharExtensions.cs`
- Modify: `tests/Bezoro.Core.Tests/Extensions/GenericExtensionsTests.cs`
- Modify: `tests/Bezoro.Core.Tests/Helpers/ExceptionHelperTests.cs`
- Modify: `tests/Bezoro.Core.Tests/Helpers/ValidationHelperTests.cs`

**Interfaces:**
- Canonical: `ThrowIf<T>(this T value, bool condition, string? paramName = null, string? message = null)`.
- Compatibility: expression overload forwards by evaluating the supplied predicate for every call.
- Compatibility types: `ExceptionHelper` and `ValidationHelper` remain public and functional.

- [ ] **Step 1: Add the per-instance expression regression test**

Add:

```csharp
[Fact]
public void ThrowIf_WhenExpressionTextIsReused_ShouldEvaluateEachInstance()
{
	static Expression<Func<int, bool>> GreaterThan(int threshold) => value => value > threshold;

	5.ThrowIf(GreaterThan(10)).Should().Be(5);
	Action action = () => 5.ThrowIf(GreaterThan(0));
	action.Should().Throw<ArgumentException>();
}
```

Run:

```powershell
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj --filter "FullyQualifiedName~GenericExtensionsTests.ThrowIf_WhenExpressionTextIsReused" --no-restore --verbosity minimal
```

Expected: the test fails because the current global cache reuses the first result for the same expression text.

- [ ] **Step 2: Make the boolean overload canonical**

Delete `ExpressionCache`, `ExpressionCacheKey`, and the `System.Collections.Concurrent` import. Mark the expression overload:

```csharp
[Obsolete("Use ThrowIf(bool, string?, string?) instead.")]
public static T ThrowIf<T>(
	this T value,
	Expression<Func<T, bool>> predicate,
	string? paramName = null,
	Exception? customException = null)
{
	predicate.ThrowIfNull();
	if (!predicate.Compile()(value)) return value;
	if (customException is not null) throw customException;

	string name = paramName ?? typeof(T).Name;
	string message = $"Condition '{predicate.Body}' failed for parameter '{name}' with value '{value}'.";
	throw new ArgumentException(message, name);
}
```

This is the existing public signature, including `paramName` and `customException`; do not narrow or otherwise change it. Keep the expression overload's exact custom-exception, parameter-name, and generated-message behavior while removing global cached state. Keep the boolean overload's exact exception type, parameter name, and message behavior. Update the boolean overload's XML docs as the primary contract.

- [ ] **Step 3: Migrate the only production caller**

In `src/Bezoro.Chess.UCI/API/Common/Extensions/CharExtensions.cs`, replace the expression call with the equivalent boolean call:

```csharp
.ThrowIf(!UciConstants.Pieces.CHARS_ALL.Contains(c));
```

Migrate general Core tests to the boolean overload. Keep one expression compatibility test inside a narrow `#pragma warning disable CS0618` / restore pair.

- [ ] **Step 4: Deprecate broad reflection helpers without deleting them**

Apply these exact type attributes:

```csharp
[Obsolete("Use direct exception construction instead.")]
public static class ExceptionHelper
```

```csharp
[Obsolete("Use focused guard extensions or direct validation instead.")]
public static class ValidationHelper
```

Do not change their public signatures or current exception contracts. Keep their tests as compatibility tests with file-local `#pragma warning disable CS0618`, and add reflection assertions that both types carry the exact `ObsoleteAttribute.Message`.

Remove `ValidationHelper`'s calls to the now-obsolete `ExceptionHelper`: construct `InvalidOperationException`, `ArgumentException`, and `ArgumentNullException` directly in the standard cases. Preserve the generic `IsFalse<TException>` compatibility overload with a private `CreateCompatibilityException<TException>(string message)` inside `ValidationHelper` that performs the same `(string)`-constructor, parameterless-constructor, then `InvalidOperationException` fallback currently provided by `ExceptionHelper`. This confines reflection to the obsolete compatibility surface and keeps production callers warning-free.

- [ ] **Step 5: Run guard/helper and coupled Chess tests**

```powershell
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj --filter "FullyQualifiedName~GenericExtensionsTests|FullyQualifiedName~ExceptionHelperTests|FullyQualifiedName~ValidationHelperTests" --no-restore --verbosity minimal
dotnet test tests/Bezoro.Chess.UCI.Tests/Bezoro.Chess.UCI.Tests.csproj --no-restore --verbosity minimal
```

Expected: all tests pass with the exact existing exception types/messages/parameter names and zero warnings.

- [ ] **Step 6: Commit after explicit authorization only**

```powershell
git add src/Bezoro.Core/Extensions/GenericExtensions.cs src/Bezoro.Core/Helpers src/Bezoro.Chess.UCI/API/Common/Extensions/CharExtensions.cs tests/Bezoro.Core.Tests/Extensions/GenericExtensionsTests.cs tests/Bezoro.Core.Tests/Helpers
git commit -m "Simplify Core guard contracts"
```

---

### Task 5: Consolidate SwapbackArray lookup and name lifecycle operations

**Files:**
- Modify: `src/Bezoro.Core/Types/SwapbackArray.cs`
- Modify: `src/Bezoro.Core/Types/Singleton.cs`
- Modify: `tests/Bezoro.Core.Tests/Types/SwapbackArrayContainsTests.cs`
- Modify: `tests/Bezoro.Core.Tests/Types/SwapbackArrayIndexOfTests.cs`
- Modify: `tests/Bezoro.Core.Tests/Types/SwapbackArrayTryIndexOfTests.cs`
- Modify: `tests/Bezoro.Core.Tests/Types/SwapbackArrayTryRemoveTests.cs`
- Modify: `tests/Bezoro.Core.Tests/Types/SwapbackArrayClearTests.cs`
- Modify: `tests/Bezoro.Core.Tests/Types/SingletonTests.cs`

**Interfaces:**
- Canonical: `TryGetIndex(T item, out uint index)`, `ClearRetainingCapacity`, `ConfigureFactoryAndRecreate`, `ResetAndDispose`.
- Compatibility: nullable-index lookup and optional-boolean lifecycle overloads remain forwarding shims.

- [ ] **Step 1: Add canonical lookup tests first**

Change general lookup tests to compile against `TryGetIndex(item, out uint index)` and assert a miss returns `false` with `index == 0`. Add a comparer-sensitive reference-type case shared across `Contains`, `IndexOf`, `TryGetIndex`, legacy `TryIndexOf`, and `TryRemove`.

Run:

```powershell
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj --filter "FullyQualifiedName~SwapbackArrayTryIndexOfTests" --no-restore --verbosity minimal
```

Expected: compilation fails because `TryGetIndex` does not exist.

- [ ] **Step 2: Introduce one private lookup kernel**

Implement `private int FindIndex(T item)` using `EqualityComparer<T>.Default` and the current `_count` bounds. Make `Contains`, `IndexOf`, canonical `TryGetIndex`, legacy nullable `TryIndexOf`, and `TryRemove` consume it. The distinct canonical name intentionally avoids making existing `TryIndexOf(item, out var index)` source calls ambiguous while the obsolete nullable overload remains available. Use:

```csharp
public bool TryGetIndex(T item, out uint index)
{
	int found = FindIndex(item);
	index = found >= 0 ? (uint)found : 0;
	return found >= 0;
}

[Obsolete("Use TryGetIndex(T, out uint) instead.")]
public bool TryIndexOf(T item, out uint? index)
{
	bool found = TryGetIndex(item, out uint value);
	index = found ? value : null;
	return found;
}
```

Preserve `IndexOf`'s current return type and not-found sentinel.

- [ ] **Step 3: Replace clear/reset booleans with named entry points**

Expose these exact pairs and shims:

```csharp
// SwapbackArray<T>
public void Clear();
public void ClearRetainingCapacity();
[Obsolete("Use Clear() or ClearRetainingCapacity() instead.")]
public void Clear(bool trim);

// Singleton<T>
public static void ConfigureFactory(Func<T> factory);
public static void ConfigureFactoryAndRecreate(Func<T> factory);
[Obsolete("Use ConfigureFactory(Func<T>) or ConfigureFactoryAndRecreate(Func<T>) instead.")]
public static void ConfigureFactory(Func<T> factory, bool recreateIfInitialized);
public static void Reset();
public static void ResetAndDispose();
[Obsolete("Use Reset() or ResetAndDispose() instead.")]
public static void Reset(bool disposeInstances);
```

Each pair calls one private boolean kernel so behavior stays identical. Migrate all repository calls: `SwapbackArray.Clear(false)` to `ClearRetainingCapacity`, `Singleton.Reset(true)` to `ResetAndDispose`, and `ConfigureFactory(factory, true)` to `ConfigureFactoryAndRecreate(factory)`. Add narrowly suppressed compatibility tests proving old `true`/`false` values forward correctly.

- [ ] **Step 4: Run collection and singleton suites**

```powershell
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj --filter "FullyQualifiedName~SwapbackArray|FullyQualifiedName~SingletonTests" --no-restore --verbosity minimal
```

Expected: all tests pass with zero warnings and existing count/capacity/version/disposal behavior.

- [ ] **Step 5: Commit after explicit authorization only**

```powershell
git add src/Bezoro.Core/Types/SwapbackArray.cs src/Bezoro.Core/Types/Singleton.cs tests/Bezoro.Core.Tests/Types/SwapbackArray*Tests.cs tests/Bezoro.Core.Tests/Types/SingletonTests.cs
git commit -m "Consolidate Core lookup and lifecycle APIs"
```

---

### Task 6: Deprecate color-name and enum-style convenience layers

**Files:**
- Modify: `src/Bezoro.Core/Extensions/StringExtensions.Colour.cs`
- Modify: `src/Bezoro.Core/Extensions/EnumExtensions.cs`
- Modify: `tests/Bezoro.Core.Tests/Extensions/StringExtensionsColourTests.cs`
- Modify: `tests/Bezoro.Core.Tests/Extensions/EnumExtensionsTests.cs`
- Modify: `src/Bezoro.Core/README.md`

**Interfaces:**
- Canonical string coloring: `Color(this string, string)` and `Color(this string, Color)`.
- Retain numeric `Color` overloads and general string formatting primitives.
- Compatibility: every named string color and every enum styling wrapper remains callable and returns identical markup.

- [ ] **Step 1: Add canonical consumer tests**

Add/retain tests proving:

```csharp
"hello".Color("red").Should().Be("<color=red>hello</color>");
"hello".Color(new Color(1f, 0f, 0f, 1f)).Should().Be("<color=#FF0000FF>hello</color>");
TestEnum.Value.Color("red").Should().Be("<color=red>Value</color>");
TestEnum.Value.Color(new Color(1f, 0f, 0f, 1f)).Should().Be("<color=#FF0000FF>Value</color>");
```

These should pass before implementation and are the preserved canonical baseline.

- [ ] **Step 2: Mark the exact string convenience family obsolete**

Add `[Obsolete("Use Color(string) or Color(Color) instead.")]` to `ColorHex` and every parameterless named-color method from `Red` through `NeonPurple` in `StringExtensions.Colour.cs`. Keep all methods and exact colors. Do not deprecate the six `Color` overloads.

- [ ] **Step 3: Mark the exact enum wrapper family obsolete**

Keep `Color(Enum, string)` and `Color(Enum, Color)` canonical. Add `[Obsolete("Use value.ToString() with the corresponding string extension instead.")]` to these enum methods only:

```text
Black, Blue, Bold, Brown, Capitalize, Cyan, Gray, Green, Italic, Lowercase,
Magenta, Orange, Purple, Red, Size, Strikethrough, Underline, Uppercase, White, Yellow
```

Update named-color enum bodies to call the applicable canonical `value.ToString().Color(color)` overload directly, not obsolete string named-color wrappers. Styling bodies may continue to call canonical string formatting methods.

- [ ] **Step 4: Preserve compatibility tests without repository warnings**

Move named-wrapper assertions into clearly labeled compatibility facts surrounded by `#pragma warning disable CS0618` and `#pragma warning restore CS0618`. Add reflection tests for the exact obsolete messages. Keep canonical tests warning-free.

- [ ] **Step 5: Update Core README**

Document `Color(string)` and `Color(Color)` as the primary color APIs, show enum formatting via `value.ToString()`, list the compatibility window, and update named lifecycle and `TryGetIndex` examples. Do not advertise obsolete wrappers as preferred APIs.

- [ ] **Step 6: Run extension tests**

```powershell
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj --filter "FullyQualifiedName~StringExtensionsColourTests|FullyQualifiedName~EnumExtensionsTests" --no-restore --verbosity minimal
```

Expected: all output-compatibility tests pass and compilation has zero obsolete warnings.

- [ ] **Step 7: Commit after explicit authorization only**

```powershell
git add src/Bezoro.Core/Extensions/StringExtensions.Colour.cs src/Bezoro.Core/Extensions/EnumExtensions.cs src/Bezoro.Core/README.md tests/Bezoro.Core.Tests/Extensions
git commit -m "Deprecate Core formatting conveniences"
```

---

### Task 7: Establish and protect focused Core benchmarks

**Files:**
- Modify: `benchmarks/Bezoro.Core.Benchmarks/SwapbackArrayBenchmarks.cs`
- Create: `benchmarks/Bezoro.Core.Benchmarks/ObjectPoolBenchmarks.cs`
- Create: `benchmarks/Bezoro.Core.Benchmarks/PooledItem.cs`

**Interfaces:**
- Benchmark names remain stable for before/after comparison.
- Setup cost is excluded from the measured SwapbackArray removal paths.

- [ ] **Step 1: Capture the current benchmark baseline in Release**

```powershell
dotnet run -c Release --project benchmarks/Bezoro.Core.Benchmarks/Bezoro.Core.Benchmarks.csproj -- --filter "*SwapbackArrayBenchmarks*"
```

Expected: BenchmarkDotNet writes a report under `BenchmarkDotNet.Artifacts/results`. Record the report path and the means/allocations for `BalancedChurn`, `GrowthChurn`, `RemovalComplexityLarge`, and `RemovalComplexitySmall` in the task report; debug runs are invalid.

- [ ] **Step 2: Move removal setup out of measured methods**

Use `[IterationSetup(Target = nameof(RemovalComplexityLarge))]` and `[IterationSetup(Target = nameof(RemovalComplexitySmall))]` to create their arrays and seeded `Random` instances. The benchmark methods must measure only the 500 removal loop. Preserve the existing named churn benchmarks and their descriptions.

- [ ] **Step 3: Add focused pool benchmarks**

Create `[MemoryDiagnoser] ObjectPoolBenchmarks` with a prewarmed `ObjectPool<PooledItem>` and these exact benchmarks:

```csharp
[Benchmark(Baseline = true, Description = "Rent and return")]
public void RentAndReturn()
{
	var item = _pool.Rent();
	_pool.Return(item);
}

[Benchmark(Description = "Rent handle and dispose")]
public void RentHandleAndDispose()
{
	var handle = _pool.RentHandle();
	handle.Dispose();
}
```

Create `internal sealed class PooledItem { }` in `PooledItem.cs`; do not nest it in the benchmark class.

- [ ] **Step 4: Run the post-change benchmarks**

```powershell
dotnet run -c Release --project benchmarks/Bezoro.Core.Benchmarks/Bezoro.Core.Benchmarks.csproj -- --filter "*SwapbackArrayBenchmarks*" "*ObjectPoolBenchmarks*"
```

Expected: reports complete. Compare the same benchmark names; do not claim a speed/allocation improvement from the setup-corrected removal numbers because the measurement boundary changed. Investigate any material regression in `BalancedChurn`, `GrowthChurn`, or `RentAndReturn`; document unavoidable identity-tracking cost explicitly.

- [ ] **Step 5: Commit after explicit authorization only**

```powershell
git add benchmarks/Bezoro.Core.Benchmarks
git commit -m "Focus Core complexity benchmarks"
```

---

### Task 8: Complete Core compatibility, documentation, and repository gates

**Files:**
- Modify: `api/PublicTypes.Bezoro.Core.txt` only if generated type entries change
- Modify: `tests/Bezoro.Core.Tests/README.md`
- Verify: all files changed by Tasks 1-7

**Interfaces:**
- Produce: zero-warning canonical repository usage and fresh dual-target/build/test/API evidence.

- [ ] **Step 1: Scan for non-compatibility obsolete usage and duplicate kernels**

```powershell
rg -n --glob '*.{cs,md}' 'ThrowIf\s*\([^,]*=>' src tests benchmarks samples
rg -n --glob '*.{cs,md}' '\.Clear\((true|false)\)|\.Reset\((true|false)\)|ConfigureFactory\([^\n]*(true|false)' src tests benchmarks samples
rg -n --glob '*.cs' 'out uint\?' src tests benchmarks samples
rg -n --glob '*.cs' 'ExpressionCache|ExpressionCacheKey|TryCreateNewItem|CreateNewItem|DiscardItem' src/Bezoro.Core
```

Expected: obsolete usage appears only in dedicated compatibility tests; global expression-cache and split pool-kernel identifiers are absent.

- [ ] **Step 2: Review XML docs and READMEs**

Confirm every new public member has `summary`, parameter, return, and exception tags as applicable. Update `tests/Bezoro.Core.Tests/README.md` to name the pool invariant, copied-handle, `TryGetIndex` lookup-kernel, and compatibility coverage.

- [ ] **Step 3: Refresh and verify public type baselines**

```powershell
./scripts/Export-PublicApi.ps1
git diff -- api/PublicTypes.Bezoro.Core.txt
./scripts/Export-PublicApi.ps1 -Check
```

Expected: `-Check` passes. Because this exporter tracks public types rather than members, no Core baseline change is expected unless an accidental public type was introduced; internal helper types must not appear.

- [ ] **Step 4: Run targeted and full verification**

```powershell
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj --no-restore --verbosity minimal
dotnet test tests/Bezoro.Chess.UCI.Tests/Bezoro.Chess.UCI.Tests.csproj --no-restore --verbosity minimal
dotnet test bezoro.framework.sln --no-restore --verbosity minimal
dotnet build bezoro.framework.sln --no-restore --verbosity minimal
dotnet build bezoro.framework.sln -c Release --no-restore --verbosity minimal
git diff --check
git status --short
```

Expected: every test passes; both Core targets compile through both solution builds with zero warnings/errors; diff check is clean; status contains only authorized work and pre-existing plan/spec files.

- [ ] **Step 5: Self-review against the approved design**

Confirm explicitly in the task report: one acquire kernel; exception-safe capacity reservation; one owned-discard kernel; copied-handle exactly-once state; accepted foreign returns; no expression cache; helper and convenience shims preserved; canonical lookup and lifecycle names; XML/README migration; release benchmark evidence; no public lease type; no dormant capability deletion.

- [ ] **Step 6: Commit after explicit authorization only**

```powershell
git add api/PublicTypes.Bezoro.Core.txt tests/Bezoro.Core.Tests/README.md
git commit -m "Document Core API simplification"
```
