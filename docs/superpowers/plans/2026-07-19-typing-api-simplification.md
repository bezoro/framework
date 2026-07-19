# Typing API Simplification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make valid typing results and atomic word consumption the obvious public APIs while retaining the current result layout, provider mutation capability, file loading behavior, and compatibility entry points.

**Architecture:** `TypingResult` keeps its existing properties and eight-parameter constructor shape, but public named factories become canonical and the constructor becomes an obsolete forwarding-compatible path. A new consumption-only `IWordSource` exposes `TryGetNextWord`; `ArrayWordProvider` remains the mutable store, `IWordProvider` remains a compatibility interface, and file I/O moves to a focused extension.

**Tech Stack:** C# 13, .NET 9, .NET Standard 2.1, xUnit, FluentAssertions, System.Text.Json, PowerShell.

## Global Constraints

- Use red-green-refactor for public APIs and behavior changes.
- Compile `Bezoro.TypingSystem` for both `net9.0` and `netstandard2.1` with zero warnings.
- Preserve `TypingResult` public property names/types, JSON shape, constructor parameter order/types, status numeric values, and validator results.
- Keep `IWordProvider`, all existing provider members, `ArrayWordProvider` mutations, insertion-order consumption, exhaustion behavior, file loading, and constructor validation available.
- Apply the exact canonical replacement messages specified below and migrate repository consumers to canonical APIs.
- Keep one top-level type per file and namespaces aligned with folders.
- Do not add randomization, asynchronous word sources, or a generalized provider hierarchy.
- Do not stage or commit without current explicit authorization.

---

### Task 1: Pin the serialized `TypingResult` contract and expose canonical factories

**Files:**
- Create: `tests/Bezoro.TypingSystem.Tests/Types/TypingResultCompatibilityTests.cs`
- Modify: `tests/Bezoro.TypingSystem.Tests/Types/TypingResultCompletedTests.cs`
- Modify: `tests/Bezoro.TypingSystem.Tests/Types/TypingResultEmptyTargetTests.cs`
- Modify: `tests/Bezoro.TypingSystem.Tests/Types/TypingResultMatchTests.cs`
- Modify: `tests/Bezoro.TypingSystem.Tests/Types/TypingResultMismatchTests.cs`
- Modify: `tests/Bezoro.TypingSystem.Tests/Types/TypingResultPositionOutOfRangeTests.cs`
- Modify: `src/Bezoro.TypingSystem/Types/TypingResult.cs`
- Modify: `src/Bezoro.TypingSystem/Bezoro.TypingSystem.csproj`

**Interfaces:**
- Canonical public factories: `Match`, `Mismatch`, `Completed`, `EmptyTarget`, `PositionOutOfRange` with their existing signatures.
- Compatibility constructor: the existing eight parameters in the existing order.

- [ ] **Step 1: Add a green pre-change shape baseline**

Create `TypingResultCompatibilityTests.cs` with a reflection assertion that the public properties and types are exactly:

```text
Expected: char
Input: char
IsComplete: bool
IsCorrect: bool
IsFaulted: bool
NextPosition: int
Position: int
Status: TypingValidationStatus
TargetLength: int
```

Add this serialization contract test. It intentionally verifies the repository's existing serialized output rather than inventing a new deserialization contract for the readonly result shape:

```csharp
[Fact]
public void JsonSerialization_WhenUsingLegacyConstructor_ShouldPreservePublicShapeAndValues()
{
#pragma warning disable CS0618
	var original = new TypingResult(TypingValidationStatus.Match, 'a', 0, 'a', true, false, 1, 3);
#pragma warning restore CS0618
	string json = JsonSerializer.Serialize(original);

	using var document = JsonDocument.Parse(json);
	document.RootElement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
		"IsComplete", "IsCorrect", "IsFaulted", "Expected", "Input", "NextPosition", "Position", "TargetLength", "Status");
	document.RootElement.GetProperty("Status").GetInt32().Should().Be((int)original.Status);
	document.RootElement.GetProperty("Expected").GetString().Should().Be(original.Expected.ToString());
	document.RootElement.GetProperty("Input").GetString().Should().Be(original.Input.ToString());
	document.RootElement.GetProperty("Position").GetInt32().Should().Be(original.Position);
	document.RootElement.GetProperty("NextPosition").GetInt32().Should().Be(original.NextPosition);
	document.RootElement.GetProperty("TargetLength").GetInt32().Should().Be(original.TargetLength);
	document.RootElement.GetProperty("IsCorrect").GetBoolean().Should().Be(original.IsCorrect);
	document.RootElement.GetProperty("IsComplete").GetBoolean().Should().Be(original.IsComplete);
}
```

Before the constructor is obsolete, omit the pragma only for the initial baseline run. Run:

```powershell
dotnet test tests/Bezoro.TypingSystem.Tests/Bezoro.TypingSystem.Tests.csproj --filter "FullyQualifiedName~TypingResultCompatibilityTests" --no-restore --verbosity minimal
```

Expected: the baseline passes. This is a compatibility characterization, not the red step.

- [ ] **Step 2: Turn existing internal-factory tests into public consumer tests**

The five existing factory test classes already use the intended signatures through the current friend-assembly boundary. Add reflection assertions using `BindingFlags.Public | BindingFlags.Static` that each named factory exists and returns `TypingResult`.

Run:

```powershell
dotnet test tests/Bezoro.TypingSystem.Tests/Bezoro.TypingSystem.Tests.csproj --filter "FullyQualifiedName~TypingResultCompletedTests|FullyQualifiedName~TypingResultEmptyTargetTests|FullyQualifiedName~TypingResultMatchTests|FullyQualifiedName~TypingResultMismatchTests|FullyQualifiedName~TypingResultPositionOutOfRangeTests" --no-restore --verbosity minimal
```

Expected red result: the direct behavior assertions still run, but the new reflection assertions fail because the factories are not public.

- [ ] **Step 3: Implement the smallest public API change**

Change the five factory declarations from `internal static` to `public static` and add complete XML docs. Convert the primary-constructor declaration to an explicit readonly struct so the constructor attribute and assignments are unambiguous. The constructor must remain exactly:

```csharp
[Obsolete("Use Match, Mismatch, Completed, EmptyTarget, or PositionOutOfRange instead.")]
public TypingResult(
	TypingValidationStatus status,
	char expected,
	byte position,
	char input,
	bool isCorrect,
	bool isComplete,
	byte nextPosition,
	byte targetLength)
```

Assign the same nine public properties from those parameters. Do not derive `IsCorrect` or `IsComplete` in this change: retaining their stored values preserves legacy constructor and serialized/reflection behavior. `IsFaulted` remains status-derived.

Remove `<InternalsVisibleTo Include="Bezoro.TypingSystem.Tests" />` from `Bezoro.TypingSystem.csproj` after confirming `rg -n 'internal ' src/Bezoro.TypingSystem` identifies no other member used by the test project. This makes the public-factory tests genuine consumer tests.

- [ ] **Step 4: Add constructor shim metadata coverage**

In `TypingResultCompatibilityTests`, locate the eight-parameter constructor by its exact parameter types and assert its `ObsoleteAttribute.Message`. Keep one constructor behavior test inside a narrow `CS0618` pragma proving caller-provided flag values are still stored unchanged.

- [ ] **Step 5: Run TypingResult and validator tests**

```powershell
dotnet test tests/Bezoro.TypingSystem.Tests/Bezoro.TypingSystem.Tests.csproj --filter "FullyQualifiedName~TypingResult|FullyQualifiedName~TypingValidator|FullyQualifiedName~TypingMetrics" --no-restore --verbosity minimal
```

Expected: all tests pass with unchanged statuses, positions, flags, callbacks, metrics, and zero warnings.

- [ ] **Step 6: Commit after explicit authorization only**

```powershell
git add src/Bezoro.TypingSystem/Types/TypingResult.cs src/Bezoro.TypingSystem/Bezoro.TypingSystem.csproj tests/Bezoro.TypingSystem.Tests/Types/TypingResult*Tests.cs
git commit -m "Expose valid typing result factories"
```

---

### Task 2: Introduce atomic word consumption and preserve provider compatibility

**Files:**
- Create: `src/Bezoro.TypingSystem/Abstractions/IWordSource.cs`
- Modify: `src/Bezoro.TypingSystem/Abstractions/IWordProvider.cs`
- Modify: `src/Bezoro.TypingSystem/Types/ArrayWordProvider.cs`
- Modify: `tests/Bezoro.TypingSystem.Tests/Types/ArrayWordProviderTests.cs`
- Create: `tests/Bezoro.TypingSystem.Tests/Types/WordProviderCompatibilityTests.cs`

**Interfaces:**
- Canonical consumption contract: `IWordSource.TryGetNextWord(out ReadOnlyMemory<char> word)`.
- Canonical mutation owner: concrete `ArrayWordProvider` methods `AddWord`, `AddWords`, `ClearWords`, `RemoveWord`, and property `WordCount`.
- Compatibility: `IWordProvider` extends `IWordSource` and retains all old members.

- [ ] **Step 1: Write canonical consumer tests first**

Replace check-then-get tests with:

```csharp
[Fact]
public void TryGetNextWord_WhenWordsExist_ShouldConsumeInInsertionOrder()
{
	IWordSource source = new ArrayWordProvider(["one", "two"]);

	source.TryGetNextWord(out var first).Should().BeTrue();
	source.TryGetNextWord(out var second).Should().BeTrue();

	first.ToString().Should().Be("one");
	second.ToString().Should().Be("two");
}

[Fact]
public void TryGetNextWord_WhenExhausted_ShouldReturnFalseAndEmptyMemory()
{
	IWordSource source = new ArrayWordProvider(["one"]);
	_ = source.TryGetNextWord(out _);

	source.TryGetNextWord(out var word).Should().BeFalse();
	word.Should().Be(ReadOnlyMemory<char>.Empty);
}
```

Run:

```powershell
dotnet test tests/Bezoro.TypingSystem.Tests/Bezoro.TypingSystem.Tests.csproj --filter "FullyQualifiedName~ArrayWordProviderTests.TryGetNextWord" --no-restore --verbosity minimal
```

Expected red result: `IWordSource` and `TryGetNextWord` do not exist.

- [ ] **Step 2: Add the narrow interface**

Create:

```csharp
namespace Bezoro.TypingSystem.Abstractions;

/// <summary>Provides atomic consumption of words for typing sessions.</summary>
public interface IWordSource
{
	/// <summary>Attempts to consume the next word.</summary>
	/// <param name="word">The consumed word, or empty memory when exhausted.</param>
	/// <returns><see langword="true" /> when a word was consumed; otherwise <see langword="false" />.</returns>
	bool TryGetNextWord(out ReadOnlyMemory<char> word);
}
```

Change `IWordProvider : IWordSource`. Do not obsolete the interface type because `ArrayWordProvider` must implement it without a source warning. Mark every old member on the interface with focused messages:

```text
HasMoreWords: Use TryGetNextWord(out ReadOnlyMemory<char>) instead.
GetNextWord: Use TryGetNextWord(out ReadOnlyMemory<char>) instead.
WordCount: Use ArrayWordProvider.WordCount instead.
AddWord: Use ArrayWordProvider.AddWord instead.
AddWords: Use ArrayWordProvider.AddWords instead.
AddWordsFromFile: Use WordProviderFileExtensions.LoadWordsFromFile instead.
ClearWords: Use ArrayWordProvider.ClearWords instead.
RemoveWord: Use ArrayWordProvider.RemoveWord instead.
```

Each attribute must use the exact message listed above, including the final `instead.`.

- [ ] **Step 3: Implement atomic consumption in ArrayWordProvider**

Add:

```csharp
public bool TryGetNextWord(out ReadOnlyMemory<char> word)
{
	if (_index >= _words.Count)
	{
		word = ReadOnlyMemory<char>.Empty;
		return false;
	}

	word = _words[_index++].AsMemory();
	return true;
}
```

Mark concrete `HasMoreWords` `[Obsolete("Use TryGetNextWord(out ReadOnlyMemory<char>) instead.")]`. Keep `WordCount` and mutation methods canonical and public. Retain explicit `IWordProvider.GetNextWord`; it forwards to `TryGetNextWord` and throws the existing `InvalidOperationException("No more words available.")` on `false`.

- [ ] **Step 4: Add narrowly scoped old-interface tests**

`WordProviderCompatibilityTests.cs` must use `#pragma warning disable CS0618` only around declarations/calls to old members. Assert:

- `HasMoreWords` matches exhaustion;
- `GetNextWord` returns the next item and preserves the exact exhaustion exception/message;
- old mutation members alter the same concrete provider;
- every old member has the exact `ObsoleteAttribute.Message` above.

- [ ] **Step 5: Run provider tests**

```powershell
dotnet test tests/Bezoro.TypingSystem.Tests/Bezoro.TypingSystem.Tests.csproj --filter "FullyQualifiedName~ArrayWordProviderTests|FullyQualifiedName~WordProviderCompatibilityTests" --no-restore --verbosity minimal
```

Expected: canonical and compatibility tests pass, consumption remains insertion-ordered, and the build emits zero warnings.

- [ ] **Step 6: Commit after explicit authorization only**

```powershell
git add src/Bezoro.TypingSystem/Abstractions src/Bezoro.TypingSystem/Types/ArrayWordProvider.cs tests/Bezoro.TypingSystem.Tests/Types/ArrayWordProviderTests.cs tests/Bezoro.TypingSystem.Tests/Types/WordProviderCompatibilityTests.cs
git commit -m "Add atomic typing word consumption"
```

---

### Task 3: Move file loading out of the provider contract and migrate consumers

**Files:**
- Create: `src/Bezoro.TypingSystem/Extensions/WordProviderFileExtensions.cs`
- Modify: `src/Bezoro.TypingSystem/Types/ArrayWordProvider.cs`
- Modify: `tests/Bezoro.TypingSystem.Tests/Types/ArrayWordProviderTests.cs`
- Modify: `tests/Bezoro.TypingSystem.Tests/Types/WordProviderCompatibilityTests.cs`
- Modify: `samples/TypingSystem.ConsoleDemo/Program.cs`
- Modify: `src/Bezoro.TypingSystem/README.md`
- Modify: `tests/Bezoro.TypingSystem.Tests/README.md`
- Modify: `src/Bezoro.TypingSystem/Bezoro.TypingSystem.csproj`

**Interfaces:**
- Canonical file adapter: `WordProviderFileExtensions.LoadWordsFromFile(this ArrayWordProvider provider, string filePath)`.
- Compatibility instance member: `ArrayWordProvider.AddWordsFromFile(string)` and `IWordProvider.AddWordsFromFile(string)`.

- [ ] **Step 1: Write the extension consumer test**

Replace the existing general file test with:

```csharp
[Fact]
public void LoadWordsFromFile_WhenFileContainsWords_ShouldAppendWords()
{
	string filePath = Path.GetTempFileName();
	File.WriteAllLines(filePath, ["two", "three"]);

	try
	{
		var provider = new ArrayWordProvider(["one"]);
		provider.LoadWordsFromFile(filePath);

		provider.WordCount.Should().Be(3);
		Drain(provider).Should().Equal("one", "two", "three");
	}
	finally
	{
		File.Delete(filePath);
	}
}
```

Define `Drain(IWordSource source)` in the test class using only `TryGetNextWord`. Run the test and expect a compile error because `LoadWordsFromFile` is absent.

- [ ] **Step 2: Implement the focused extension**

Create:

```csharp
namespace Bezoro.TypingSystem.Extensions;

public static class WordProviderFileExtensions
{
	public static void LoadWordsFromFile(this ArrayWordProvider provider, string filePath)
	{
		if (provider is null) throw new ArgumentNullException(nameof(provider));
		if (filePath is null) throw new ArgumentNullException(nameof(filePath));

		foreach (string word in File.ReadLines(filePath))
			provider.AddWord(word.AsMemory());
	}
}
```

Add complete XML docs and document `ArgumentNullException`, `ArgumentException`, `FileNotFoundException`, `DirectoryNotFoundException`, `IOException`, `SecurityException`, and `UnauthorizedAccessException` where they can flow from argument/path validation or `File.ReadLines` on the supported targets.

- [ ] **Step 3: Forward the legacy instance member**

Mark `ArrayWordProvider.AddWordsFromFile`:

```csharp
[Obsolete("Use WordProviderFileExtensions.LoadWordsFromFile instead.")]
public void AddWordsFromFile(string filePath) => this.LoadWordsFromFile(filePath);
```

Use the same message on `IWordProvider.AddWordsFromFile`. Keep a compatibility test proving it appends the same words.

- [ ] **Step 4: Migrate the sample to atomic consumption**

Change the sample declaration to `IWordSource wordSource`. Replace:

```csharp
while (wordProvider.HasMoreWords)
{
	var wordMemory = wordProvider.GetNextWord();
```

with:

```csharp
while (wordSource.TryGetNextWord(out var wordMemory))
{
```

No other sample behavior changes.

- [ ] **Step 5: Update README contracts**

In the source README:

- identify `IWordSource` as the consumption contract;
- show a `while (source.TryGetNextWord(out var word))` quick start;
- describe mutations as `ArrayWordProvider` responsibilities;
- show `provider.LoadWordsFromFile(path)` from `Bezoro.TypingSystem.Extensions`;
- document the obsolete compatibility window;
- list public `TypingResult` factories as the valid creation API.

Update the test README to describe atomic exhaustion, file-adapter cleanup, constructor serialization, and compatibility-shim coverage.

Prove there are no Logging symbols before removing the unused project edge:

```powershell
rg -n --glob '*.cs' 'Bezoro\.Logging|using Bezoro\.Logging' src/Bezoro.TypingSystem
```

Expected: no matches. Remove `<ProjectReference Include="..\Bezoro.Logging\Bezoro.Logging.csproj" />` from `Bezoro.TypingSystem.csproj`. Keep the demonstrated Core reference used by `SwapbackArray<T>` and Core guards.

- [ ] **Step 6: Build the sample and test the project**

```powershell
dotnet test tests/Bezoro.TypingSystem.Tests/Bezoro.TypingSystem.Tests.csproj --no-restore --verbosity minimal
dotnet build samples/TypingSystem.ConsoleDemo/TypingSystem.ConsoleDemo.csproj --no-restore --verbosity minimal
```

Expected: tests pass, the sample builds without obsolete warnings, and file tests clean up temporary files.

- [ ] **Step 7: Commit after explicit authorization only**

```powershell
git add src/Bezoro.TypingSystem/Extensions src/Bezoro.TypingSystem/Types/ArrayWordProvider.cs src/Bezoro.TypingSystem/Bezoro.TypingSystem.csproj src/Bezoro.TypingSystem/README.md tests/Bezoro.TypingSystem.Tests samples/TypingSystem.ConsoleDemo/Program.cs
git commit -m "Separate typing file loading from consumption"
```

---

### Task 4: Complete Typing API, target, and repository verification

**Files:**
- Modify: `api/PublicTypes.Bezoro.TypingSystem.txt`
- Verify: `src/Bezoro.TypingSystem/Bezoro.TypingSystem.csproj`
- Verify: every file changed by Tasks 1-3

**Interfaces:**
- Produce: a baseline containing `IWordSource` and `WordProviderFileExtensions`, with all canonical consumers warning-free.

- [ ] **Step 1: Scan canonical consumers and compatibility boundaries**

```powershell
rg -n --glob '*.{cs,md}' 'IWordProvider|\.HasMoreWords|\.GetNextWord\(|\.AddWordsFromFile\(' src tests benchmarks samples
rg -n --glob '*.cs' 'new TypingResult\(' src tests benchmarks samples
rg -n --glob '*.cs' '#pragma warning disable CS0618' tests/Bezoro.TypingSystem.Tests
```

Expected: old provider APIs and constructor use occur only in `IWordProvider.cs`, forwarding implementations, and dedicated compatibility tests. General tests, source, README examples, and sample use factories/`IWordSource`/the file extension.

- [ ] **Step 2: Regenerate and inspect the public type baseline**

```powershell
./scripts/Export-PublicApi.ps1
git diff -- api/PublicTypes.Bezoro.TypingSystem.txt
./scripts/Export-PublicApi.ps1 -Check
```

Expected diff adds exactly:

```text
Bezoro.TypingSystem.Abstractions.IWordSource | public interface | review-contract | Abstractions/IWordSource.cs
Bezoro.TypingSystem.Extensions.WordProviderFileExtensions | public static class | stable-shape | Extensions/WordProviderFileExtensions.cs
```

No existing public type disappears.

- [ ] **Step 3: Verify both library targets explicitly**

```powershell
dotnet build src/Bezoro.TypingSystem/Bezoro.TypingSystem.csproj -f net9.0 --no-restore --verbosity minimal
dotnet build src/Bezoro.TypingSystem/Bezoro.TypingSystem.csproj -f netstandard2.1 --no-restore --verbosity minimal
```

Expected: both builds succeed with zero warnings/errors.

- [ ] **Step 4: Run full tests and solution builds**

```powershell
dotnet test tests/Bezoro.TypingSystem.Tests/Bezoro.TypingSystem.Tests.csproj --no-restore --verbosity minimal
dotnet test bezoro.framework.sln --no-restore --verbosity minimal
dotnet build bezoro.framework.sln --no-restore --verbosity minimal
dotnet build bezoro.framework.sln -c Release --no-restore --verbosity minimal
git diff --check
git status --short
```

Expected: all tests and builds pass with zero warnings; diff check is clean; only authorized files plus pre-existing plans/spec are present.

- [ ] **Step 5: Self-review against the approved design**

Record direct evidence for: five public factories; obsolete raw constructor; unchanged serialized/reflection shape; status/position behavior; one-method consumption interface; concrete mutations retained; atomic exhaustion; old provider members forward; file I/O moved; sample/docs migrated; both targets and public API baseline pass; no async/random/generalized hierarchy added.

- [ ] **Step 6: Commit after explicit authorization only**

```powershell
git add api/PublicTypes.Bezoro.TypingSystem.txt
git commit -m "Update Typing public API baseline"
```
