# ECS Complexity Simplification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the ECS public vocabulary and fluent query surface unambiguous, then remove duplicate traversal, executor, accessor, and scalar-mutation machinery without removing compatibility or dormant capability and without regressing measured hot paths.

**Architecture:** `World` remains the engine-agnostic owner and disposal boundary; `WorldEntityStore` owns entity-location and component-column resolution; `QueryView<TSpec>` owns fluent query execution; `QueryCursor` and compiled handles remain the low-level escape hatch; `CommandStream` is the canonical deferred-mutation surface. Compatibility members remain thin, documented `[Obsolete]` forwarders. Direct iteration continues to use arity-specialized chunk loops, but query preparation and executor contracts are shared.

**Tech Stack:** C#/.NET 9, .NET Standard 2.1, Roslyn incremental generators on .NET Standard 2.0, xUnit, FluentAssertions, BenchmarkDotNet, PowerShell, MSBuild.

## Global Constraints

- Follow red-green-refactor TDD for every behavior or public API change: add the consumer-facing test, run it and observe the named failure, implement only enough to pass, then refactor.
- For semantic-preserving internal refactors, run the named characterization tests before and after; do not invent a failing test for unchanged behavior.
- Keep all superseded public types and members available with the exact `[Obsolete]` messages in this plan; do not set `error: true`.
- Do not obsolete `World.TryGet`, `World.ForEach`, `QueryCursor`, compiled handles, dormant query recognizers, boxed snapshot capability, batch mutation paths, or any other surface not explicitly approved here.
- Preserve `net9.0` and `netstandard2.1` for `Bezoro.ECS`; preserve `netstandard2.0` for `Bezoro.ECS.SourceGen`.
- Preserve nullable/analyzer warnings-as-errors and XML documentation generation. The final build must report zero warnings and zero errors.
- Keep namespaces aligned with folders and add at most one top-level type per new source file.
- Preserve stable entity versions, changed/added query semantics, fixed-capacity behavior, query ownership validation, disposal ordering, scheduler conflict inference, and system callback dispatch behavior.
- Keep arity-specific executors and loops for one through four components. Consolidate orchestration, not typed hot-path bodies.
- Do not remove legacy/dormant generator recognizers solely because repository searches are empty. Removal requires separate history and downstream-usage evidence and is outside this plan.
- BenchmarkDotNet evidence must come from Release reliable runs. A throughput regression over 5%, a new steady-state allocation, or unstable entity-version behavior blocks completion pending explicit review.
- Treat unrelated worktree changes as user-owned. Inspect the diff after every task and never stage or commit without current explicit authorization.

---

### Task 1: Add representative benchmark cases and capture the before baseline

**Files:**
- Modify: `benchmarks/Bezoro.ECS.Benchmarks/EcsWorldQueryViewBenchmarks.cs`
- Modify: `benchmarks/Bezoro.ECS.Benchmarks/EcsWorldHotPathBenchmarks.cs`
- Modify: `benchmarks/Bezoro.ECS.Benchmarks/README.md`

**Interfaces:**
- Consumes: current `QueryView<TSpec>.ForEach`, `QueryView<TSpec>.RunParallelEntity`, and direct `World.Run` implementations.
- Produces: named baseline cases for managed mutable traversal, parallel entity-aware traversal, and the sequential executor path that will be consolidated.

- [ ] **Step 1: Add the managed mutable and parallel entity-aware benchmark cases before changing runtime code**

Add these exact methods to `EcsWorldQueryViewBenchmarks`:

```csharp
[Benchmark(Description = "QueryView mutable ForEach over managed components")]
public int QueryViewForEachManaged()
{
	_managedQuery.ForEach<ManagedNote>(
		static (Entity entity, ref ManagedNote note) =>
		{
			_ = entity;
			note.Count++;
		}
	);

	return _world.EntityCount;
}

[Benchmark(Description = "QueryView parallel entity-aware struct job")]
public int QueryViewRunParallelEntity()
{
	_positionVelocityQuery.RunParallelEntity<IntegrateEntityJob, Position, Velocity>(new(0.016f), 4);
	return _world.EntityCount;
}
```

Change `EcsWorldHotPathBenchmarks.QueryForEachDirect` to exercise the canonical fluent path while preserving the same compiled query and job:

```csharp
[Benchmark(Description = "QueryView direct struct-job Run")]
public void QueryForEachDirect()
{
	_world.Query<PositionVelocityQuerySpec>().Run<IntegrateJob, Position, Velocity>(new(0.016f));
}
```

Expected: benchmark code compiles against the current runtime. These are measurement cases, not pass/fail unit tests.

- [ ] **Step 2: Document what each case gates**

Update the benchmark table and release-gate notes so `EcsWorldQueryViewBenchmarks` explicitly names managed read-only, managed mutable, sequential job, entity-aware job, and parallel entity-aware job coverage. Keep the existing 5% review threshold and add: “Entity-aware cases must preserve valid entity IDs/versions and must not add entity-scratch materialization.”

- [ ] **Step 3: Build the benchmark project**

Run:

```powershell
dotnet build benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj -c Release --no-restore --verbosity minimal
```

Expected: both referenced ECS targets compile with zero warnings/errors.

- [ ] **Step 4: Capture the reliable before reports**

Run before any runtime edit:

```powershell
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj -- --reliable --filter "*EcsWorldQueryViewBenchmarks*"
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj -- --reliable --filter "*EcsWorldHotPathBenchmarks*"
```

Expected: `BenchmarkDotNet.Artifacts/results/Bezoro.ECS.Benchmarks.EcsWorldQueryViewBenchmarks-report-github.md` and `...EcsWorldHotPathBenchmarks-report-github.md` contain successful measurements. Copy the means and allocated bytes for every named case into the task report; do not commit generated artifacts.

- [ ] **Step 5: Commit after explicit authorization only**

```powershell
git add benchmarks/Bezoro.ECS.Benchmarks/EcsWorldQueryViewBenchmarks.cs benchmarks/Bezoro.ECS.Benchmarks/EcsWorldHotPathBenchmarks.cs benchmarks/Bezoro.ECS.Benchmarks/README.md
git commit -m "Benchmark ECS simplification paths"
```

Expected: benchmark instrumentation only. Skip unless the current user explicitly authorizes staging and committing.

---

### Task 2: Establish canonical component/resource access and correct scheduler metadata

**Files:**
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldApiContractTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Internal/GeneratedSystemMetadataResolverTests.cs`
- Create: `tests/Bezoro.ECS.Tests/Services/LegacyGetComponentSystem.cs`
- Create: `tests/Bezoro.ECS.Tests/Services/LegacyGetResourceSystem.cs`
- Create: `tests/Bezoro.ECS.Tests/Services/LegacyTryGetManagedSystem.cs`
- Modify: `src/Bezoro.ECS/Abstractions/IWorld.cs`
- Modify: `src/Bezoro.ECS/Services/World.cs`
- Modify: `src/Bezoro.ECS.SourceGen/Generators/SystemMetadataGenerator.cs`

**Interfaces:**
- Consumes: existing `Read`, `TryRead`, `Write`, `TryWrite`, `ReadResource`, `TryReadResource`, and `WriteResource` behavior.
- Produces: canonical implementation ownership plus compatibility shims: mutable `Get` and `GetResource` are obsolete writes; duplicate `TryGetManaged` is an obsolete read-copy alias. `TryGet` is retained because the approved design did not authorize its deprecation.

- [ ] **Step 1: Write failing public-contract tests**

Add reflection assertions to `WorldApiContractTests` for these exact messages on both `World` and `IWorld` where the member exists:

```csharp
private const string GET_OBSOLETE_MESSAGE =
	"Use Read<T>(Entity) for read-only access or Write<T>(Entity) for mutable access instead.";
private const string GET_RESOURCE_OBSOLETE_MESSAGE =
	"Use ReadResource<T>() for read-only access or WriteResource<T>() for mutable access instead.";
private const string TRY_GET_MANAGED_OBSOLETE_MESSAGE =
	"Use TryRead<T>(Entity, out T) instead.";

[Theory]
[InlineData(typeof(World), "Get", GET_OBSOLETE_MESSAGE)]
[InlineData(typeof(IWorld), "Get", GET_OBSOLETE_MESSAGE)]
[InlineData(typeof(World), "GetResource", GET_RESOURCE_OBSOLETE_MESSAGE)]
[InlineData(typeof(IWorld), "GetResource", GET_RESOURCE_OBSOLETE_MESSAGE)]
[InlineData(typeof(World), "TryGetManaged", TRY_GET_MANAGED_OBSOLETE_MESSAGE)]
public void LegacyAccessMember_WhenInspectingContract_ShouldPointToCanonicalApi(
	Type declaringType,
	string memberName,
	string expectedMessage)
{
	var member = declaringType.GetMethods()
		.Single(method => method.Name == memberName && method.IsGenericMethodDefinition);

	member.GetCustomAttribute<ObsoleteAttribute>()!.Message.Should().Be(expectedMessage);
}
```

Add these imports at the top of `WorldApiContractTests.cs`:

```csharp
using System.Reflection;
using Bezoro.ECS.Abstractions;
```

Run:

```powershell
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj --no-restore --filter "FullyQualifiedName~WorldApiContractTests.LegacyAccessMember" --verbosity minimal
```

Expected RED: the assertion fails because the members do not yet carry `ObsoleteAttribute`.

- [ ] **Step 2: Write failing generator inference fixtures and tests**

Create one top-level fixture per file. Use narrow warning suppression because compatibility usage is the behavior under test:

```csharp
// LegacyGetComponentSystem.cs
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Tests.Services;

internal sealed class LegacyGetComponentSystem(Entity entity) : ISystem
{
	public void Update(in SystemContext context)
	{
#pragma warning disable CS0618
		ref var component = ref context.World.Get<ErgonomicJobPosition>(entity);
#pragma warning restore CS0618
		component.Value++;
	}
}
```

```csharp
// LegacyGetResourceSystem.cs
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Tests.Services;

internal sealed class LegacyGetResourceSystem : ISystem
{
	public void Update(in SystemContext context)
	{
#pragma warning disable CS0618
		ref var resource = ref context.World.GetResource<ErgonomicSchedulerResource>();
#pragma warning restore CS0618
		_ = resource;
	}
}
```

```csharp
// LegacyTryGetManagedSystem.cs
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Tests.Services;

internal sealed class LegacyTryGetManagedSystem(Entity entity) : ISystem
{
	public void Update(in SystemContext context)
	{
#pragma warning disable CS0618
		_ = context.World.TryGetManaged(entity, out ErgonomicReadOnlyNote _);
#pragma warning restore CS0618
	}
}
```

Add tests to `GeneratedSystemMetadataResolverTests`:

```csharp
using Bezoro.ECS.Tests.Services;
```

```csharp
[Theory]
[InlineData(typeof(LegacyGetComponentSystem), typeof(ErgonomicJobPosition))]
public void TryGet_WhenSystemUsesLegacyMutableComponentAlias_ShouldClassifyWrite(
	Type systemType,
	Type componentType)
{
	var resolver = new GeneratedSystemMetadataResolver();

	resolver.TryGet(systemType, out var metadata).Should().BeTrue();
	metadata.Writes.Should().Contain(componentType);
	metadata.Reads.Should().NotContain(componentType);
}

[Fact]
public void TryGet_WhenSystemUsesLegacyMutableResourceAlias_ShouldClassifyWrite()
{
	var resolver = new GeneratedSystemMetadataResolver();

	resolver.TryGet(typeof(LegacyGetResourceSystem), out var metadata).Should().BeTrue();
	metadata.WriteResources.Should().Contain(typeof(ErgonomicSchedulerResource));
	metadata.ReadResources.Should().NotContain(typeof(ErgonomicSchedulerResource));
}

[Fact]
public void TryGet_WhenSystemUsesLegacyManagedCopyAlias_ShouldClassifyRead()
{
	var resolver = new GeneratedSystemMetadataResolver();

	resolver.TryGet(typeof(LegacyTryGetManagedSystem), out var metadata).Should().BeTrue();
	metadata.Reads.Should().Contain(typeof(ErgonomicReadOnlyNote));
	metadata.Writes.Should().NotContain(typeof(ErgonomicReadOnlyNote));
}
```

Run:

```powershell
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj --no-restore --filter "FullyQualifiedName~GeneratedSystemMetadataResolverTests.TryGet_WhenSystemUsesLegacy" --verbosity minimal
```

Expected RED: `Get` and `GetResource` appear in read sets, and `TryGetManaged` is absent.

- [ ] **Step 3: Move implementation ownership to canonical methods and add obsolete forwarders**

In `IWorld`, apply the exact attributes and XML guidance:

```csharp
[Obsolete("Use Read<T>(Entity) for read-only access or Write<T>(Entity) for mutable access instead.")]
ref T Get<T>(Entity entity) where T : struct;

[Obsolete("Use ReadResource<T>() for read-only access or WriteResource<T>() for mutable access instead.")]
ref T GetResource<T>() where T : notnull;
```

In `World`, make `TryRead`, `Write`, and `WriteResource` contain the real implementation and make aliases one-expression forwarders:

```csharp
public bool TryRead<T>(Entity entity, out T component) where T : struct
{
	ThrowIfDisposed();
	component = default;
	if (!IsAliveUnchecked(entity))
		return false;

	int typeId = GetOrCreateComponentTypeId<T>();
	return _entityStore.TryGetComponentUnchecked(entity.Id, typeId, out component);
}

public bool TryGet<T>(Entity entity, out T component) where T : struct =>
	TryRead(entity, out component);

[Obsolete("Use TryRead<T>(Entity, out T) instead.")]
public bool TryGetManaged<T>(Entity entity, out T component) where T : struct =>
	TryRead(entity, out component);

public ref T Write<T>(Entity entity) where T : struct
{
	ThrowIfDisposed();
	EnsureAlive(entity);
	int typeId = GetOrCreateComponentTypeId<T>();
	ref var component = ref _entityStore.GetComponentRefUnchecked<T>(entity.Id, typeId);
	TrackPotentialSingleRefWrite(entity.Id, typeId);
	return ref component;
}

[Obsolete("Use Read<T>(Entity) for read-only access or Write<T>(Entity) for mutable access instead.")]
public ref T Get<T>(Entity entity) where T : struct => ref Write<T>(entity);

public ref T WriteResource<T>() where T : notnull
{
	ThrowIfDisposed();
	return ref _resources.Get<T>();
}

public ref readonly T ReadResource<T>() where T : notnull
{
	ThrowIfDisposed();
	return ref _resources.Get<T>();
}

[Obsolete("Use ReadResource<T>() for read-only access or WriteResource<T>() for mutable access instead.")]
public ref T GetResource<T>() where T : notnull => ref WriteResource<T>();
```

Retain current exception types and disposal ordering. Do not route `Read` through `Write`.

- [ ] **Step 4: Correct generator classification without deleting recognizers**

Change only the access-name switch in `SystemMetadataGenerator.TryGetDirectAccess`:

```csharp
case "Read":
case "TryRead":
case "TryGet":
case "TryGetManaged":
case "Has":
	target  = AccessTarget.Component;
	isWrite = false;
	return true;
case "Get":
case "Write":
case "TryWrite":
case "Set":
case "Replace":
case "Add":
case "Remove":
	target  = AccessTarget.Component;
	isWrite = true;
	return true;
case "ReadResource":
case "TryReadResource":
case "HasResource":
	target  = AccessTarget.Resource;
	isWrite = false;
	return true;
case "GetResource":
case "WriteResource":
case "GetOrCreateResource":
case "SetResource":
case "ReplaceResource":
case "RemoveResource":
	target  = AccessTarget.Resource;
	isWrite = true;
	return true;
```

Do not remove `LegacyQuery`, `ForEachRw`, `ForEachRW`, direct-world, or cursor recognizers in this task.

- [ ] **Step 5: Run green tests and both project builds**

```powershell
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj --no-restore --filter "FullyQualifiedName~WorldApiContractTests.LegacyAccessMember|FullyQualifiedName~GeneratedSystemMetadataResolverTests.TryGet_WhenSystemUsesLegacy" --verbosity minimal
dotnet build src/Bezoro.ECS/Bezoro.ECS.csproj --no-restore --verbosity minimal
dotnet build src/Bezoro.ECS.SourceGen/Bezoro.ECS.SourceGen.csproj --no-restore --verbosity minimal
```

Expected GREEN: all new tests pass; ECS builds `net9.0` and `netstandard2.1`; SourceGen builds `netstandard2.0`; zero warnings/errors.

- [ ] **Step 6: Commit after explicit authorization only**

```powershell
git add tests/Bezoro.ECS.Tests/Services/WorldApiContractTests.cs tests/Bezoro.ECS.Tests/Internal/GeneratedSystemMetadataResolverTests.cs tests/Bezoro.ECS.Tests/Services/LegacyGetComponentSystem.cs tests/Bezoro.ECS.Tests/Services/LegacyGetResourceSystem.cs tests/Bezoro.ECS.Tests/Services/LegacyTryGetManagedSystem.cs src/Bezoro.ECS/Abstractions/IWorld.cs src/Bezoro.ECS/Services/World.cs src/Bezoro.ECS.SourceGen/Generators/SystemMetadataGenerator.cs
git commit -m "Clarify ECS access intent"
```

Expected: canonical access ownership, compatibility attributes, and inference regression coverage. Skip without explicit authorization.

---

### Task 3: Deprecate `WorldOptions` without changing its adapter behavior

**Files:**
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldApiContractTests.cs`
- Modify: `src/Bezoro.ECS/Options/WorldOptions.cs`
- Modify: `src/Bezoro.ECS/Services/World.cs`

**Interfaces:**
- Consumes: current `WorldOptions` → `WorldConfig` adapter mapping.
- Produces: `WorldConfig` as the canonical configuration type while preserving `ChunkCapacity`, default-capacity, and `MaxDegreeOfParallelism` mapping. `ChunkSizeInBytes` remains a documented ignored compatibility property and never alters fixed-capacity construction.

- [ ] **Step 1: Add failing attribute and adapter contract tests**

Add:

```csharp
[Fact]
public void WorldOptions_WhenInspectingContract_ShouldPointToWorldConfig()
{
	typeof(WorldOptions).GetCustomAttribute<ObsoleteAttribute>()!.Message
		.Should().Be("Use WorldConfig instead.");

	typeof(World).GetConstructor([typeof(WorldOptions)])!
		.GetCustomAttribute<ObsoleteAttribute>()!.Message
		.Should().Be("Use World(WorldConfig) instead.");
}

#pragma warning disable CS0618
[Fact]
public void WorldOptions_WhenChunkSizeInBytesChanges_ShouldKeepFixedDefaultChunkCapacity()
{
	using var world = new World(new WorldOptions { ChunkSizeInBytes = 1 });
	world.Spawn(new ApiPosition());
	world.Spawn(new ApiPosition());

	var diagnostics = world.GetQueryDiagnostics(world.Compile<PositionQuerySpec>());

	diagnostics.MatchingChunkCount.Should().Be(1);
}
#pragma warning restore CS0618
```

Add this second adapter test beside it to prove the positive override remains mapped:

```csharp
#pragma warning disable CS0618
[Fact]
public void WorldOptions_WhenChunkCapacityIsPositive_ShouldMapCapacityExactly()
{
	using var world = new World(new WorldOptions { ChunkCapacity = 1, ChunkSizeInBytes = 64 * 1024 });
	world.Spawn(new ApiPosition());
	world.Spawn(new ApiPosition());

	var diagnostics = world.GetQueryDiagnostics(world.Compile<PositionQuerySpec>());

	diagnostics.MatchingChunkCount.Should().Be(2);
}
#pragma warning restore CS0618
```

Run:

```powershell
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj --no-restore --filter "FullyQualifiedName~WorldApiContractTests.WorldOptions" --verbosity minimal
```

Expected RED: attribute assertions fail; adapter behavior assertion should already pass and is a compatibility characterization.

- [ ] **Step 2: Add exact obsolete attributes and truthful XML docs**

```csharp
[Obsolete("Use WorldConfig instead.")]
public sealed class WorldOptions
```

```csharp
[Obsolete("Use World(WorldConfig) instead.")]
public World(WorldOptions options) : this(ToWorldConfig(options)) { }
```

Replace the `ChunkSizeInBytes` XML summary with:

```csharp
/// <summary>
///     Gets or initializes the legacy byte-size hint. This value is retained for source compatibility and is ignored;
///     fixed-capacity construction uses <see cref="ChunkCapacity" /> or the <see cref="WorldConfig" /> default.
/// </summary>
```

Keep the adapter exact:

```csharp
#pragma warning disable CS0618 // Compatibility adapter necessarily names WorldOptions.
private static WorldConfig ToWorldConfig(WorldOptions options)
{
	if (options is null) throw new ArgumentNullException(nameof(options));

	return new()
	{
		ChunkCapacity = options.ChunkCapacity > 0 ? options.ChunkCapacity : 256,
		MaxDegreeOfParallelism = options.MaxDegreeOfParallelism
	};
}
#pragma warning restore CS0618
```

- [ ] **Step 3: Run focused tests**

```powershell
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj --no-restore --filter "FullyQualifiedName~WorldApiContractTests.WorldOptions" --verbosity minimal
```

Expected GREEN: both attributes carry exact messages and both fixed-capacity adapter characterizations pass.

- [ ] **Step 4: Commit after explicit authorization only**

```powershell
git add tests/Bezoro.ECS.Tests/Services/WorldApiContractTests.cs src/Bezoro.ECS/Options/WorldOptions.cs src/Bezoro.ECS/Services/World.cs
git commit -m "Deprecate legacy ECS world options"
```

Expected: compatibility-only deprecation with no runtime mapping change. Skip without explicit authorization.

---

### Task 4: Make `CommandStream` canonical through system execution

**Files:**
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldApiContractTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/SystemManagerTests.cs`
- Modify: `src/Bezoro.ECS/Types/CommandBuffer.cs`
- Modify: `src/Bezoro.ECS/Types/SystemContext.cs`
- Modify: `src/Bezoro.ECS/Services/World.cs`
- Modify: `src/Bezoro.ECS/Internal/SystemBatchExecutor.cs`
- Modify: `src/Bezoro.GameSystems/TimerSystem/Services/TimerSystem.cs`
- Modify: `src/Bezoro.GameSystems/HealthSystem/Services/HealthSystem.cs`
- Modify: `src/Bezoro.GameSystems/ActivationSystem/Services/ActivationProcessingSystem.cs`
- Modify: `src/Bezoro.GameSystems/ActivationSystem/Services/ActivationIngestionSystem.cs`

**Interfaces:**
- Consumes: `CommandStream` recording/playback and current `CommandBuffer` translation behavior.
- Produces: `SystemContext.CommandStream` and `World.CreateCommandStream()` as canonical; `CommandBuffer`, `SystemContext.Commands`, the old context constructor, `CreateCommandBuffer`, and `BeginCommands` remain obsolete forwarders.

- [ ] **Step 1: Replace the old reflection expectation with failing canonical/compatibility assertions**

Replace `PublicSystemSurface_WhenInspectingContracts_ShouldExposeCommandBufferOnSystemContext` with:

```csharp
[Fact]
public void PublicSystemSurface_WhenInspectingContracts_ShouldExposeCanonicalCommandStream()
{
	var commandStreamProperty = typeof(SystemContext).GetProperty("CommandStream");
	commandStreamProperty.Should().NotBeNull();
	commandStreamProperty!.PropertyType.Should().Be(typeof(CommandStream));

	typeof(SystemContext).GetProperty(nameof(SystemContext.Commands))!
		.GetCustomAttribute<ObsoleteAttribute>()!.Message
		.Should().Be("Use CommandStream instead.");

	typeof(CommandBuffer).GetCustomAttribute<ObsoleteAttribute>()!.Message
		.Should().Be("Use CommandStream instead.");
}

[Theory]
[InlineData(nameof(World.CreateCommandBuffer))]
[InlineData(nameof(World.BeginCommands))]
public void LegacyCommandCreationAlias_WhenInspectingContract_ShouldPointToCreateCommandStream(string memberName)
{
	var method = typeof(World).GetMethod(memberName)!;
	method.GetCustomAttribute<ObsoleteAttribute>()!.Message
		.Should().Be("Use CreateCommandStream instead.");
}
```

Run the two tests. Expected RED: `CommandStream` property and attributes do not exist.

- [ ] **Step 2: Add the canonical context constructor/property and compatibility shims**

Replace the primary-constructor struct with this shape, retaining complete XML docs for every public member:

```csharp
public readonly struct SystemContext
{
	public SystemContext(float deltaTime, Stage stage, World world, CommandStream commandStream)
	{
		DeltaTime = deltaTime;
		Stage = stage;
		World = world ?? throw new ArgumentNullException(nameof(world));
		CommandStream = commandStream ?? throw new ArgumentNullException(nameof(commandStream));
	}

	[Obsolete("Use SystemContext(float, Stage, World, CommandStream) instead.")]
	public SystemContext(float deltaTime, Stage stage, World world, CommandBuffer commands)
		: this(deltaTime, stage, world, (CommandStream)commands) { }

	public CommandStream CommandStream { get; }

	[Obsolete("Use CommandStream instead.")]
	public CommandBuffer Commands => new(CommandStream);

	public float DeltaTime { get; }
	public Stage Stage { get; }
	public World World { get; }
}
```

Add:

```csharp
[Obsolete("Use CommandStream instead.")]
public readonly struct CommandBuffer(CommandStream stream)
```

Add exact attributes to `World`:

```csharp
[Obsolete("Use CreateCommandStream instead.")]
public CommandBuffer CreateCommandBuffer() => new(CreateCommandStream());

[Obsolete("Use CreateCommandStream instead.")]
public CommandStream BeginCommands() => CreateCommandStream();
```

- [ ] **Step 3: Make the scheduler construct canonical context directly**

In `SystemBatchExecutor.ExecuteSystem`, replace only the buffer wrapping with the canonical constructor; the stream remains owned by `FlushStreams`:

```csharp
var stream = world.CreateCommandStream();
streams[index] = stream;
var context = new SystemContext(execution.DeltaTime, execution.State.Stage, world, stream);
execution.State.System.Update(in context);
```

Preserve the existing playback/disposal `finally` in `FlushStreams`; do not dispose the stream inside `ExecuteSystem`.

- [ ] **Step 4: Migrate repository-owned systems and tests to `context.CommandStream`**

Use `CommandStream` parameter types and `context.CommandStream` in the listed GameSystems files. In `SystemManagerTests`, rename compatibility-specific local test names only where they assert the canonical path, and use `CommandStream` for captured references and diagnostics. Keep one narrow compatibility assertion in `WorldApiContractTests` for the obsolete constructor/property under `#pragma warning disable CS0618`:

```csharp
#pragma warning disable CS0618
using var stream = world.CreateCommandStream();
var context = new SystemContext(0.5f, Stage.Tick, world, new CommandBuffer(stream));
((CommandStream)context.Commands).Should().BeSameAs(stream);
#pragma warning restore CS0618
```

- [ ] **Step 5: Run green tests and affected-project tests**

```powershell
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj --no-restore --filter "FullyQualifiedName~WorldApiContractTests|FullyQualifiedName~SystemManagerTests" --verbosity minimal
dotnet test tests/Bezoro.GameSystems.Tests/Bezoro.GameSystems.Tests.csproj --no-restore --verbosity minimal
```

Expected GREEN: exact attributes pass, stream identity is preserved, stale-stream and playback lifecycle tests pass, all GameSystems tests pass, zero warnings/errors.

- [ ] **Step 6: Commit after explicit authorization only**

```powershell
git add tests/Bezoro.ECS.Tests/Services/WorldApiContractTests.cs tests/Bezoro.ECS.Tests/Services/SystemManagerTests.cs src/Bezoro.ECS/Types/CommandBuffer.cs src/Bezoro.ECS/Types/SystemContext.cs src/Bezoro.ECS/Services/World.cs src/Bezoro.ECS/Internal/SystemBatchExecutor.cs src/Bezoro.GameSystems/TimerSystem/Services/TimerSystem.cs src/Bezoro.GameSystems/HealthSystem/Services/HealthSystem.cs src/Bezoro.GameSystems/ActivationSystem/Services/ActivationProcessingSystem.cs src/Bezoro.GameSystems/ActivationSystem/Services/ActivationIngestionSystem.cs
git commit -m "Make command streams canonical"
```

Expected: canonical scheduler/system usage plus compatibility forwarders. Skip without explicit authorization.

---

### Task 5: Traverse managed `QueryView` components directly and preserve read-only tracking

**Files:**
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldErgonomicApiTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldRuntimeAllocationTests.cs`
- Create: `tests/Bezoro.ECS.Tests/Services/ChangedErgonomicManagedNoteQuery.cs`
- Modify: `src/Bezoro.ECS/Types/QueryView.cs`
- Modify: `src/Bezoro.ECS/Internal/QueryChunkWalker.cs`
- Modify: `src/Bezoro.ECS/Internal/WorldDirectIterationService.cs`
- Modify: `src/Bezoro.ECS/Services/World.cs`

**Interfaces:**
- Consumes: `ArchetypeStorage` chunk columns and stable version arrays.
- Produces: one direct chunk route for managed and unmanaged `struct` components, plus a distinct read-only walker that never marks changed data.

- [ ] **Step 1: Add/confirm characterization tests before refactoring**

Add a changed-filter test proving read-only managed traversal does not mark changes, and a mutable test proving direct-style behavior across multiple chunks:

```csharp
[Fact]
public void QueryView_ForEachRead_WhenManagedComponentIsObserved_ShouldNotMarkItChanged()
{
	using var world = CreateWorld(chunkCapacity: 1);
	world.Spawn(new ErgonomicManagedNote { Label = "first", Count = 1 });
	world.Spawn(new ErgonomicManagedNote { Label = "second", Count = 2 });
	var changed = world.Compile<ChangedErgonomicManagedNoteQuery>();
	using (var initial = world.Execute(changed))
		initial.MoveNext().Should().BeTrue();

	world.Query<ErgonomicManagedNoteQuery>().ForEachRead<ErgonomicManagedNote>(
		static (Entity entity, in ErgonomicManagedNote note) =>
		{
			_ = entity;
			_ = note.Count;
		}
	);

	using var after = world.Execute(changed);
	after.MoveNext().Should().BeTrue();
	after.Current.Length.Should().Be(0);
}

[Fact]
public void QueryView_ForEach_WhenManagedComponentSpansChunks_ShouldMutateInPlace()
{
	using var world = CreateWorld(chunkCapacity: 1);
	var first = world.Spawn(new ErgonomicManagedNote { Label = "first", Count = 1 });
	var second = world.Spawn(new ErgonomicManagedNote { Label = "second", Count = 2 });

	world.Query<ErgonomicManagedNoteQuery>().ForEach<ErgonomicManagedNote>(
		static (Entity _, ref ErgonomicManagedNote note) => note.Count++
	);

	world.Read<ErgonomicManagedNote>(first).Count.Should().Be(2);
	world.Read<ErgonomicManagedNote>(second).Count.Should().Be(3);
}
```

Create the changed-filter query as its own top-level type:

```csharp
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Tests.Services;

internal readonly struct ChangedErgonomicManagedNoteQuery : ICompiledQuerySpec
{
	public void Build(ref QueryBuilder builder)
	{
		builder.All<ErgonomicManagedNote>();
		builder.Changed<ErgonomicManagedNote>();
	}
}
```

Add this helper method inside `WorldErgonomicApiTests` so both tests use an explicit multi-chunk configuration:

```csharp
private static World CreateWorld(int chunkCapacity) =>
	new(
		new WorldConfig
		{
			EntityCapacity = 16,
			ComponentTypeCapacity = 16,
			CommandCapacity = 32,
			CommandPayloadCapacityPerType = 32,
			QueryResultCapacity = 16,
			ChunkCapacity = chunkCapacity
		}
	);
```

Run both tests before edits.

Expected: both pass on the existing fallback path; they are characterization evidence for a semantic-preserving refactor.

- [ ] **Step 2: Add an explicit read-only one-component walker**

In `QueryChunkWalker`, factor the existing entity-aware one-component body into:

```csharp
public static void ExecuteReadOnlyEntity<TAction, T1>(
	World world, QueryChunkMatch[] chunkMatches, int chunkMatchCount, TAction action)
	where TAction : struct, IEntityChunkAction<T1>
	where T1 : struct =>
	ExecuteEntityCore<TAction, T1>(world, chunkMatches, chunkMatchCount, action, trackWrites: false);

public static void ExecuteEntity<TAction, T1>(
	World world, QueryChunkMatch[] chunkMatches, int chunkMatchCount, TAction action)
	where TAction : struct, IEntityChunkAction<T1>
	where T1 : struct =>
	ExecuteEntityCore<TAction, T1>(world, chunkMatches, chunkMatchCount, action, trackWrites: true);

private static void ExecuteEntityCore<TAction, T1>(
	World world, QueryChunkMatch[] chunkMatches, int chunkMatchCount, TAction action, bool trackWrites)
	where TAction : struct, IEntityChunkAction<T1>
	where T1 : struct
{
	int typeId1 = world.GetOrCreateComponentTypeId<T1>();
	if (trackWrites)
		world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);

	int cachedArchetypeId = int.MinValue;
	ArchetypeStorage? cachedArchetype = null;
	int cachedColumnIndex1 = -1;
	var versions = world.GetEntityVersionsForCursor();
	for (var i = 0; i < chunkMatchCount; i++)
	{
		var match = chunkMatches[i];
		if (match.ArchetypeId != cachedArchetypeId)
		{
			cachedArchetypeId = match.ArchetypeId;
			cachedArchetype = world.GetArchetypeForCursor(match.ArchetypeId);
			cachedColumnIndex1 = GetColumnIndex(cachedArchetype, typeId1, match.ArchetypeId);
		}

		var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
		if (match.Count == 0)
			continue;

		ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
		ref var entityIdStart = ref chunk.EntityIds[match.RowStart];
		for (var offset = 0; offset < match.Count; offset++)
		{
			int entityId = Unsafe.Add(ref entityIdStart, offset);
			action.Invoke(new(entityId, versions[entityId]), ref Unsafe.Add(ref c1Start, offset));
		}
	}
}
```

The executor must branch once before iteration, not once per entity. Keep the existing `EntityIds` plus version-array construction.

- [ ] **Step 3: Add the read-only orchestration route**

Add `WorldDirectIterationService.ExecuteDirectReadOnlyEntityAction<TSpec,TAction,T1>` using the same validation, chunk-match acquisition, query fill, and `finally` release as `ExecuteDirectEntityAction`, but call `QueryChunkWalker.ExecuteReadOnlyEntity`. Add the matching internal `World.ExecuteDirectReadOnlyEntityAction` forwarding method.

Exact call at the service core:

```csharp
QueryChunkWalker.ExecuteReadOnlyEntity<TAction, T1>(_world, chunkMatches, chunkMatchCount, action);
```

- [ ] **Step 4: Remove managed materialization branches from `QueryView`**

Delete `using System.Runtime.CompilerServices;`, delete `TypeTraits<T>`, and replace every typed body after null validation with its direct call. The one-component examples are:

```csharp
_world.ExecuteDirectReadOnlyEntityAction<TQuery, ReadOnlyEntityAction<T1>, T1>(_handle, new(action));
```

```csharp
_world.ExecuteDirectEntityAction<TQuery, EntityAction<T1>, T1>(_handle, new(action));
```

Apply the existing arity-specialized direct calls for two, three, and four components unconditionally. Delete all `Execute`, `cursor.Current`, `_world.Read`, and `_world.Write` fallback loops. Update delegate/member XML from “unmanaged component” to “component” for `ForEach` APIs; job APIs remain unmanaged.

- [ ] **Step 5: Add steady-state allocation coverage for managed direct traversal**

In `WorldRuntimeAllocationTests`, mirror the existing unmanaged `QueryView_ForEach` warm-up/measurement pattern with the existing managed struct containing a `string` and a mutable `int`. Assert:

```csharp
allocated.Should().Be(0);
```

Run the allocation test before the refactor and record whether the fallback already meets zero allocation; the required result after the refactor is zero. This test is a performance contract, not proof by itself that materialization was removed.

- [ ] **Step 6: Verify focused behavior and allocation tests**

```powershell
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj --no-restore --filter "FullyQualifiedName~QueryView_ForEachRead_WhenManaged|FullyQualifiedName~QueryView_ForEach_WhenManaged|FullyQualifiedName~WorldRuntimeAllocationTests.QueryView_ForEach_WhenExecutingRepeatedlyAfterWarmup_ShouldNotAllocateForManagedComponents" --verbosity minimal
```

Expected: all pass, managed mutable/read-only traversal is correct across chunks, read-only access does not mark changes, steady state allocates 0 B.

- [ ] **Step 7: Commit after explicit authorization only**

```powershell
git add tests/Bezoro.ECS.Tests/Services/WorldErgonomicApiTests.cs tests/Bezoro.ECS.Tests/Services/WorldRuntimeAllocationTests.cs tests/Bezoro.ECS.Tests/Services/ChangedErgonomicManagedNoteQuery.cs src/Bezoro.ECS/Types/QueryView.cs src/Bezoro.ECS/Internal/QueryChunkWalker.cs src/Bezoro.ECS/Internal/WorldDirectIterationService.cs src/Bezoro.ECS/Services/World.cs
git commit -m "Traverse managed ECS queries directly"
```

Expected: direct managed traversal with explicit read-only tracking. Skip without explicit authorization.

---

### Task 6: Make `QueryView<TSpec>` the canonical job-execution surface

**Files:**
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldApiContractTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/GeneratedQueryAndJobSourceGenIntegrationTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/SystemManagerErgonomicInferenceTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldRuntimeQueryTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldRuntimeAllocationTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldAdvancedApiTests.cs`
- Modify: `benchmarks/Bezoro.ECS.Benchmarks/EcsWorldComponentAccessBenchmarks.cs`
- Modify: `src/Bezoro.ECS/Types/QueryView.cs`
- Modify: `src/Bezoro.ECS/Services/World.cs`
- Modify: `src/Bezoro.ECS.SourceGen/Generators/ForEachJobSourceGenerator.cs`

**Interfaces:**
- Consumes: exact `QueryView.Run`, `RunEntity`, `RunParallel`, and `RunParallelEntity` replacements for arities one through four.
- Produces: obsolete `World` job families as compatibility forwarders, canonical generated QueryView execution, and retained low-level cursor/compiled-query APIs. `World.ForEach` stays non-obsolete because `QueryView` has no exact entity-less delegate replacement.

- [ ] **Step 1: Add failing obsolete-contract coverage for all sixteen World job methods**

Add:

```csharp
[Theory]
[InlineData("Run", "Use QueryView<TSpec>.Run(job) instead.")]
[InlineData("RunEntity", "Use QueryView<TSpec>.RunEntity(job) instead.")]
[InlineData("RunParallel", "Use QueryView<TSpec>.RunParallel(job, degreeOfParallelism) instead.")]
[InlineData("RunParallelEntity", "Use QueryView<TSpec>.RunParallelEntity(job, degreeOfParallelism) instead.")]
public void WorldJobFamily_WhenInspectingContract_ShouldPointToQueryView(
	string methodName,
	string expectedMessage)
{
	var methods = typeof(World).GetMethods()
		.Where(method => method.Name == methodName && method.IsGenericMethodDefinition)
		.ToArray();

	methods.Should().HaveCount(4);
	methods.Should().AllSatisfy(method =>
		method.GetCustomAttribute<ObsoleteAttribute>()!.Message.Should().Be(expectedMessage));
}
```

Run it. Expected RED: all sixteen methods lack the attribute.

- [ ] **Step 2: Route QueryView directly to internal World kernels before obsoleting public methods**

Keep existing sequential internal `RunDirectFast` and `RunDirectFastEntity` arity families. Add internal `RunParallelDirect` and `RunParallelDirectEntity` arity-one-through-four families in `World`, each forwarding to `WorldDirectIterationService`. Update `QueryView` bodies to call these internal methods:

```csharp
_world.RunDirectFast<TQuery, TJob, T1>(_handle, job);
_world.RunDirectFastEntity<TQuery, TJob, T1>(_handle, job);
_world.RunParallelDirect<TQuery, TJob, T1>(_handle, job, degreeOfParallelism);
_world.RunParallelDirectEntity<TQuery, TJob, T1>(_handle, job, degreeOfParallelism);
```

Repeat with exact generic argument lists for arities two, three, and four. Do not make `QueryView` call an obsolete public method.

- [ ] **Step 3: Convert public World job methods to obsolete forwarders**

For each arity, preserve signature, constraints, XML parameters, handle validation/disposal behavior, and apply the corresponding exact attribute. Representative sequential and parallel bodies:

```csharp
[Obsolete("Use QueryView<TSpec>.Run(job) instead.")]
public void Run<TSpec, TJob, T1>(QueryHandle<TSpec> handle, TJob job)
	where TSpec : struct, ICompiledQuerySpec
	where TJob : struct, IForEach<T1>
	where T1 : unmanaged
{
	ThrowIfDisposed();
	RunDirectFast<TSpec, TJob, T1>(handle, job);
}

[Obsolete("Use QueryView<TSpec>.RunParallelEntity(job, degreeOfParallelism) instead.")]
public void RunParallelEntity<TSpec, TJob, T1>(
	QueryHandle<TSpec> handle, TJob job, int? degreeOfParallelism = null)
	where TSpec : struct, ICompiledQuerySpec
	where TJob : struct, IForEachEntity<T1>
	where T1 : unmanaged
{
	ThrowIfDisposed();
	RunParallelDirectEntity<TSpec, TJob, T1>(handle, job, degreeOfParallelism);
}
```

- [ ] **Step 4: Make generated World extensions use the canonical QueryView**

In `ForEachJobSourceGenerator.BuildSource`, preserve generated World extensions and their exact supplied handle semantics, but change their body template from `world.Run...(handle, job)` to:

```csharp
new global::Bezoro.ECS.Types.QueryView<TSpec>(world, handle)
	.Run<GeneratedJob, Component1, Component2>(job);
```

or, for entity-aware jobs:

```csharp
new global::Bezoro.ECS.Types.QueryView<TSpec>(world, handle)
	.RunEntity<GeneratedEntityJob, Component1, Component2>(job);
```

The constructed QueryView preserves the caller's compiled handle and reaches the same internal validation. Do not ignore/recompile the handle and do not emit an obsolete call from generated code.

- [ ] **Step 5: Migrate repository-owned direct World job call sites**

Replace direct `world.Run*<TSpec,...>(handle, ...)` usage in the listed tests/benchmarks with `world.Query<TSpec>().Run*<...>(...)` when a fresh type-cached handle is semantically equivalent. Where a test owns an incremental `Changed`/`Added` handle or specifically verifies handle ownership, preserve that exact handle with:

```csharp
new QueryView<ChangedPositionQuerySpec>(world, handle)
	.Run<RecordingPositionJob, Position>(new(visited));
```

Preserve cursor job tests as low-level coverage. In `SystemManagerErgonomicInferenceTests`, rename “WorldJobs” tests/systems to “QueryViewJobs” unless they specifically test the retained generated World compatibility extension. Keep one generated World extension test under a narrow `#pragma warning disable CS0618` only if the generated extension itself is marked obsolete; otherwise it should compile warning-free and route canonically.

- [ ] **Step 6: Run generator integration, query, scheduler, and allocation tests**

```powershell
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj --no-restore --filter "FullyQualifiedName~WorldApiContractTests.WorldJobFamily|FullyQualifiedName~GeneratedQueryAndJobSourceGenIntegrationTests|FullyQualifiedName~SystemManagerErgonomicInferenceTests|FullyQualifiedName~WorldRuntimeQueryTests|FullyQualifiedName~WorldRuntimeAllocationTests|FullyQualifiedName~WorldAdvancedApiTests" --verbosity minimal
```

Expected GREEN: exact obsolete messages pass; generated cursor, QueryView, and World compatibility extensions preserve behavior; canonical QueryView paths remain allocation-free where currently guaranteed.

- [ ] **Step 7: Commit after explicit authorization only**

```powershell
git add tests/Bezoro.ECS.Tests/Services/WorldApiContractTests.cs tests/Bezoro.ECS.Tests/Services/GeneratedQueryAndJobSourceGenIntegrationTests.cs tests/Bezoro.ECS.Tests/Services/SystemManagerErgonomicInferenceTests.cs tests/Bezoro.ECS.Tests/Services/WorldRuntimeQueryTests.cs tests/Bezoro.ECS.Tests/Services/WorldRuntimeAllocationTests.cs tests/Bezoro.ECS.Tests/Services/WorldAdvancedApiTests.cs benchmarks/Bezoro.ECS.Benchmarks/EcsWorldComponentAccessBenchmarks.cs src/Bezoro.ECS/Types/QueryView.cs src/Bezoro.ECS/Services/World.cs src/Bezoro.ECS.SourceGen/Generators/ForEachJobSourceGenerator.cs
git commit -m "Make query views the canonical job surface"
```

Expected: canonical fluent execution plus all public compatibility families. Skip without explicit authorization.

---

### Task 7: Remove parallel entity materialization and merge sequential executor shells

**Files:**
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldAdvancedApiTests.cs`
- Create: `tests/Bezoro.ECS.Tests/Services/ParallelEntityProbeComponent.cs`
- Create: `tests/Bezoro.ECS.Tests/Services/ParallelEntityProbeQuery.cs`
- Create: `tests/Bezoro.ECS.Tests/Services/ParallelEntityHandleProbeJob.cs`
- Modify: `src/Bezoro.ECS/Internal/WorldDirectIterationService.cs`

**Interfaces:**
- Consumes: stable `chunk.EntityIds`, world version array, chunk-match scratch, and arity-specific job executors.
- Produces: direct entity construction in parallel workers and one sequential executor interface/validated shell for direct and entity-aware jobs.

- [ ] **Step 1: Establish characterization for entity identity and steady-state allocations**

Create these one-type-per-file fixtures:

```csharp
// ParallelEntityProbeComponent.cs
namespace Bezoro.ECS.Tests.Services;

internal struct ParallelEntityProbeComponent
{
	public int Index;
}
```

```csharp
// ParallelEntityProbeQuery.cs
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Tests.Services;

internal readonly struct ParallelEntityProbeQuery : ICompiledQuerySpec
{
	public void Build(ref QueryBuilder builder) => builder.All<ParallelEntityProbeComponent>();
}
```

```csharp
// ParallelEntityHandleProbeJob.cs
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Tests.Services;

internal readonly struct ParallelEntityHandleProbeJob(Entity[] observed)
	: IForEachEntity<ParallelEntityProbeComponent>
{
	public void Execute(Entity entity, ref ParallelEntityProbeComponent component1) =>
		observed[component1.Index] = entity;
}
```

Add this exact test to `WorldAdvancedApiTests`:

```csharp
[Fact]
public void RunParallelEntity_WhenEntitiesSpanChunks_ShouldPreserveStableHandles()
{
	using var world = new World(
		new WorldConfig
		{
			EntityCapacity = 32,
			ComponentTypeCapacity = 8,
			CommandCapacity = 64,
			CommandPayloadCapacityPerType = 64,
			QueryResultCapacity = 32,
			ChunkCapacity = 1,
			MaxDegreeOfParallelism = 4
		}
	);
	var expected = new Entity[16];
	for (var i = 0; i < expected.Length; i++)
		expected[i] = world.Spawn(new ParallelEntityProbeComponent { Index = i });

	var observed = new Entity[expected.Length];
	world.Query<ParallelEntityProbeQuery>()
		.RunParallelEntity<ParallelEntityHandleProbeJob, ParallelEntityProbeComponent>(new(observed), 4);

	observed.Should().Equal(expected);
}
```

Each worker writes a distinct array index, so no shared-index synchronization is required. Use the Task 1 BenchmarkDotNet case, rather than a unit-test allocation assertion, for parallel scheduler allocation/throughput evidence.

Run:

```powershell
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj --no-restore --filter "FullyQualifiedName~RunParallelEntity_WhenEntitiesSpanChunks_ShouldPreserveStableHandles" --verbosity minimal
```

Expected: the behavior test passes before refactoring; Task 1 already records the performance baseline.

- [ ] **Step 2: Construct parallel entities from chunks and stable versions**

In all four `RunParallelEntity` overloads:

- Replace `AcquireQueryExecutionScratchForDirectIteration(out var chunkMatches, out var entities, ...)` with `AcquireQueryChunkMatchScratchForDirectIteration(out var chunkMatches, ...)`.
- Delete `_world.MaterializeQueryEntities(...)` and `match.EntityStartIndex` usage.
- Capture `var versions = _world.GetEntityVersionsForCursor();` before worker scheduling.
- Inside each worker, add `ref var entityIdStart = ref chunk.EntityIds[match.RowStart];`.
- Build the handle per row:

```csharp
int entityId = Unsafe.Add(ref entityIdStart, offset);
var entity = new Entity(entityId, versions[entityId]);
local.Execute(entity, ref c1, in c2);
```

- Release only chunk-match scratch in `finally`.

Repeat the exact entity construction for arities one, three, and four while retaining their current typed refs and `in` arguments.

- [ ] **Step 3: Merge identical sequential executor contracts and shells**

Delete `IDirectEntityChunkExecutor`. Change every `DirectEntityJobExecutor<...>` to implement `IDirectChunkExecutor`. Change all `RunDirectFastEntity` overloads to call `ExecuteDirectValidated`. Delete `ExecuteDirectEntityValidated` completely.

The remaining single constraint is:

```csharp
private void ExecuteDirectValidated<TSpec, TExecutor>(QueryHandle<TSpec> handle, ref TExecutor executor)
	where TSpec : struct, ICompiledQuerySpec
	where TExecutor : struct, IDirectChunkExecutor
```

Retain all eight arity-specific executor structs and their typed loops.

- [ ] **Step 4: Run focused tests and structural scans**

```powershell
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj --no-restore --filter "FullyQualifiedName~RunParallel|FullyQualifiedName~RunEntity|FullyQualifiedName~QueryViewRun" --verbosity minimal
rg -n 'IDirectEntityChunkExecutor|ExecuteDirectEntityValidated|MaterializeQueryEntities|EntityStartIndex' src/Bezoro.ECS/Internal/WorldDirectIterationService.cs
```

Expected: tests pass; structural scan returns no matches in `WorldDirectIterationService.cs`; arity-specific executor structs remain.

- [ ] **Step 5: Commit after explicit authorization only**

```powershell
git add tests/Bezoro.ECS.Tests/Services/WorldAdvancedApiTests.cs tests/Bezoro.ECS.Tests/Services/ParallelEntityProbeComponent.cs tests/Bezoro.ECS.Tests/Services/ParallelEntityProbeQuery.cs tests/Bezoro.ECS.Tests/Services/ParallelEntityHandleProbeJob.cs src/Bezoro.ECS/Internal/WorldDirectIterationService.cs
git commit -m "Simplify ECS direct iteration executors"
```

Expected: no entity scratch/materialization and one validated sequential executor shell. Skip without explicit authorization.

---

### Task 8: Delegate accessor resolution to `WorldEntityStore`

**Files:**
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldRuntimeAccessorTests.cs`
- Modify: `src/Bezoro.ECS/Services/World.cs`

**Interfaces:**
- Consumes: existing `WorldEntityStore.HasComponentForAccessor`, `TryGetComponentReference`, and `ResolveComponentReference` methods.
- Produces: `World` as disposal/null guard plus delegation only; the store retains and owns the location/column-cache state machine.

- [ ] **Step 1: Add disposal-order characterization**

Add:

```csharp
[Fact]
public void GetAccessor_WhenWorldIsDisposed_ShouldThrowObjectDisposedException()
{
	var world = new World(new WorldConfig());
	var accessor = world.GetAccessor<Position>();
	world.Dispose();

	var act = () => accessor.Has(Entity.None);

	act.Should().Throw<ObjectDisposedException>();
}
```

Run all `WorldRuntimeAccessorTests` before the refactor. Expected: pass.

- [ ] **Step 2: Replace facade-owned resolution with guarded delegation**

For accessor methods in `World`, preserve existing null argument ordering where applicable, call `ThrowIfDisposed()`, then delegate:

```csharp
internal bool HasComponentForAccessor(
	Entity entity, int typeId, ref int cachedArchetypeId, ref int cachedColumnIndex)
{
	ThrowIfDisposed();
	return _entityStore.HasComponentForAccessor(
		entity, typeId, ref cachedArchetypeId, ref cachedColumnIndex);
}
```

Apply the same pattern to try-read/write and resolve methods, passing all cache refs through unchanged. Delete `World.TryResolveAccessorColumnIndex`. Do not move disposal guards into the store and do not delete the store helper.

- [ ] **Step 3: Run accessor behavior and allocation coverage**

```powershell
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj --no-restore --filter "FullyQualifiedName~WorldRuntimeAccessorTests|FullyQualifiedName~WorldRuntimeAllocationTests.GetAccessor" --verbosity minimal
rg -n 'TryResolveAccessorColumnIndex' src/Bezoro.ECS/Services/World.cs
```

Expected: all accessor tests pass, repeated access remains 0 B after warm-up, and the structural scan returns no World-local helper.

- [ ] **Step 4: Commit after explicit authorization only**

```powershell
git add tests/Bezoro.ECS.Tests/Services/WorldRuntimeAccessorTests.cs src/Bezoro.ECS/Services/World.cs
git commit -m "Delegate ECS accessor resolution"
```

Expected: store-owned resolver with World-owned lifecycle guard. Skip without explicit authorization.

---

### Task 9: Centralize scalar overwrite and change tracking

**Files:**
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldRuntimeQueryTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldRuntimePlaybackTests.cs`
- Modify: `src/Bezoro.ECS/Internal/WorldEntityStore.cs`

**Interfaces:**
- Consumes: scalar generic set/replace paths and current changed-query version bookkeeping.
- Produces: one aggressively inlined generic overwrite kernel for existing components; batch and boxed paths remain specialized.

- [ ] **Step 1: Establish scalar-path characterization**

Add these scalar-route tests to `WorldRuntimeQueryTests`:

```csharp
[Fact]
public void Set_WhenOverwritingExistingComponent_ShouldAdvanceChangedWindowOnce() =>
	AssertScalarOverwriteTracksChange(
		static (world, entity) => world.Set(entity, new Position { X = 20, Y = 30 }));

[Fact]
public void PlaybackSet_WhenOverwritingExistingComponent_ShouldAdvanceChangedWindowOnce() =>
	AssertScalarOverwriteTracksChange(
		static (world, entity) =>
		{
			using var commands = world.CreateCommandStream();
			commands.Set(entity, new Position { X = 20, Y = 30 });
			world.Playback(commands);
		});

private static void AssertScalarOverwriteTracksChange(Action<World, Entity> overwrite)
{
	using var world = new World();
	var entity = world.Spawn(new Position { X = 1, Y = 2 });
	var changed = world.Compile<ChangedPositionQuerySpec>();
	using (var initial = world.Execute(changed))
		initial.MoveNext().Should().BeTrue();

	overwrite(world, entity);

	using (var current = world.Execute(changed))
	{
		current.MoveNext().Should().BeTrue();
		current.Current.Length.Should().Be(1);
		current.Current[0].Should().Be(entity);
	}

	using var after = world.Execute(changed);
	after.MoveNext().Should().BeTrue();
	after.Current.Length.Should().Be(0);
}
```

Add this batch-route test to the `WorldRuntimeTests` partial class in `WorldRuntimePlaybackTests.cs`; it reuses the private `Position` and `ChangedPositionQuerySpec` types declared in `WorldRuntimeTests.cs`:

```csharp
[Fact]
public void PlaybackSetBatch_WhenOverwritingExistingComponents_ShouldAdvanceChangedWindowOnce()
{
	using var world = new World();
	var first = world.Spawn(new Position { X = 1, Y = 2 });
	var second = world.Spawn(new Position { X = 3, Y = 4 });
	var changed = world.Compile<ChangedPositionQuerySpec>();
	using (var initial = world.Execute(changed))
		initial.MoveNext().Should().BeTrue();

	using (var commands = world.CreateCommandStream())
	{
		commands.Set(first, new Position { X = 10, Y = 20 });
		commands.Set(second, new Position { X = 30, Y = 40 });
		world.Playback(commands);
	}

	using (var current = world.Execute(changed))
	{
		current.MoveNext().Should().BeTrue();
		current.Current.ToArray().Should().BeEquivalentTo([first, second]);
	}

	using var after = world.Execute(changed);
	after.MoveNext().Should().BeTrue();
	after.Current.Length.Should().Be(0);
}
```

Run all three tests before edits. Expected: pass; these are characterization tests for unchanged scalar and batch semantics.

- [ ] **Step 2: Add one aggressively inlined overwrite kernel**

Add to `WorldEntityStore`:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private void OverwriteComponent<T>(
	int entityId,
	Fixed.EntityLocation location,
	ArchetypeStorage archetype,
	int typeId,
	in T component)
	where T : struct
{
	int columnIndex = archetype.GetColumnIndexOrNegative(typeId);
	if (columnIndex < 0)
		throw new InvalidOperationException(
			$"Type id '{typeId}' does not exist in archetype '{archetype.Id}'.");

	var chunk = archetype.GetChunkUnchecked(location.ChunkIndex);
	ref var existing = ref archetype.GetRefByIndex<T>(chunk, columnIndex, location.RowIndex);
	existing = component;
	uint changeVersion = AdvanceComponentChangeVersion();
	archetype.MarkComponentChanged(chunk, columnIndex, changeVersion);
	MarkComponentChanged(entityId, typeId, changeVersion);
}
```

Add this import to `WorldEntityStore.cs`:

```csharp
using System.Runtime.CompilerServices;
```

- [ ] **Step 3: Route scalar generic overwrite branches through the kernel**

Use `OverwriteComponent` in exactly these existing-component branches:

- `ApplySetFromCommandKnownTransition` when source and target archetypes match.
- `MoveEntityToArchetypeWithSet` when `targetArchetypeId == sourceArchetype.Id`.
- `SetComponentInternal` when `sourceArchetype.HasType(typeId)`.

Delete `MoveEntityToArchetypeWithSetKnownTransition` and have its only caller invoke `MoveEntityToArchetypeWithSet` directly.

Do not route `ApplySetBatchFromCommandKnownTransitionFast` through the scalar helper because its one-version-per-batch/chunk-span behavior is intentional. Do not merge boxed snapshot paths into the generic kernel.

- [ ] **Step 4: Run scalar, playback, changed/added, and burst tests**

```powershell
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj --no-restore --filter "FullyQualifiedName~Changed|FullyQualifiedName~ApplySet|FullyQualifiedName~Playback|FullyQualifiedName~Set_When" --verbosity minimal
rg -n 'MoveEntityToArchetypeWithSetKnownTransition' src/Bezoro.ECS/Internal/WorldEntityStore.cs
```

Expected: tests pass and the deleted forwarding helper has no matches. Batch behavior remains covered by existing burst/playback tests.

- [ ] **Step 5: Commit after explicit authorization only**

```powershell
git add tests/Bezoro.ECS.Tests/Services/WorldRuntimeQueryTests.cs tests/Bezoro.ECS.Tests/Services/WorldRuntimePlaybackTests.cs src/Bezoro.ECS/Internal/WorldEntityStore.cs
git commit -m "Centralize ECS scalar overwrites"
```

Expected: one scalar overwrite kernel, unchanged specialized batch and boxed paths. Skip without explicit authorization.

---

### Task 10: Migrate repository-owned ECS consumers off obsolete vocabulary

**Files:**
- Modify: `src/Bezoro.GameSystems/ActivationSystem/Extensions/ActivationWorldExtensions.cs`
- Modify: `src/Bezoro.GameSystems/ActivationSystem/Services/ActivationDispatchSystem.cs`
- Modify: `src/Bezoro.GameSystems/ActivationSystem/Services/ActivationIngestionSystem.cs`
- Modify: `src/Bezoro.GameSystems/InputSystem/Extensions/InputWorldExtensions.cs`
- Modify: `src/Bezoro.GameSystems/InputSystem/Services/InputIngestionSystem.cs`
- Modify: `src/Bezoro.GameSystems/InputSystem/Services/IntentToVelocitySystem.cs`
- Modify: `tests/Bezoro.GameSystems.Tests/ActivationSystem/ActivationPipelineTests.cs`
- Modify: `tests/Bezoro.GameSystems.Tests/InputSystem/InputMovementSystemsTests.cs`
- Modify: `tests/Bezoro.GameSystems.Tests/MovementSystem/MovementSystemTests.cs`
- Modify: `tests/Bezoro.GameSystems.Tests/StreamingSystem/StreamingSystemTests.cs`
- Modify: `tests/Bezoro.GameSystems.Tests/TimerSystem/TimerSystemTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/GeneratedQueryAndJobSourceGenIntegrationTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/QueryGeneratorTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/SystemManagerTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldAdvancedApiTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldApiContractTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldRuntimeAccessorTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldRuntimeOverflowTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldRuntimePlaybackTests.cs`
- Modify: `tests/Bezoro.ECS.Tests/Services/WorldRuntimeQueryTests.cs`
- Modify: `benchmarks/Bezoro.ECS.Benchmarks/EcsWorldComponentAccessBenchmarks.cs`

**Interfaces:**
- Consumes: canonical API decisions from Tasks 2–6.
- Produces: warning-free repository-owned usage; obsolete names remain only in their declarations, reflection/compatibility tests, and migration documentation.

- [ ] **Step 1: Classify every old access by intent**

For each `.Get<T>` occurrence:

- Use `Read<T>` when the result is copied/inspected only.
- Use `Write<T>` only when a `ref` is taken or the component is mutated in place.
- Keep `QueryCursor.Get<T>` unchanged; it is a distinct retained low-level API.

For each `.GetResource<T>` occurrence:

- Use `ReadResource<T>` for inspection.
- Use `WriteResource<T>` for mutation or mutable-ref capture.

For `TryGetManaged`, use `TryRead`. For `WorldOptions`, construct an equivalent `WorldConfig`, setting `MaxDegreeOfParallelism` and existing fixed-capacity defaults explicitly where tests rely on them.

- [ ] **Step 2: Apply the migrations and keep compatibility tests narrowly suppressed**

Representative replacements:

```csharp
var position = world.Read<Position>(entity);
ref var queue = ref world.WriteResource<InputCommandQueue>();
world.WriteResource<StreamingEventsResource>().Clear();
world.TryRead(entity, out ManagedTag resolved);
```

Only tests that intentionally invoke an obsolete forwarder may use:

```csharp
#pragma warning disable CS0618
// one compatibility invocation
#pragma warning restore CS0618
```

- [ ] **Step 3: Prove no accidental obsolete use remains**

```powershell
rg -n --glob '*.cs' '\.(Get|GetResource|TryGetManaged)<' src tests benchmarks samples
rg -n --glob '*.cs' '\.(CreateCommandBuffer|BeginCommands)\(' src tests benchmarks samples
rg -n --glob '*.cs' 'new World\(new WorldOptions|context\.Commands\b' src tests benchmarks samples
```

Expected: matches are only `QueryCursor.Get`, compatibility fixtures/tests, declarations, and unrelated APIs. Inspect every match; do not rely on name-only filtering.

- [ ] **Step 4: Test affected consumers**

```powershell
dotnet test tests/Bezoro.GameSystems.Tests/Bezoro.GameSystems.Tests.csproj --no-restore --verbosity minimal
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj --no-restore --verbosity minimal
```

Expected: all tests pass with zero warnings/errors.

- [ ] **Step 5: Commit after explicit authorization only**

```powershell
git add src/Bezoro.GameSystems tests/Bezoro.GameSystems.Tests tests/Bezoro.ECS.Tests benchmarks/Bezoro.ECS.Benchmarks/EcsWorldComponentAccessBenchmarks.cs
git commit -m "Migrate ECS consumers to canonical APIs"
```

Expected: repository consumer migration only. Skip without explicit authorization.

---

### Task 11: Update XML docs, project READMEs, and API compatibility evidence

**Files:**
- Modify: `src/Bezoro.ECS/README.md`
- Modify: `src/Bezoro.ECS.SourceGen/README.md`
- Modify: `tests/Bezoro.ECS.Tests/README.md`
- Modify: `benchmarks/Bezoro.ECS.Benchmarks/README.md`
- Verify: `api/PublicTypes.Bezoro.ECS.txt`
- Verify: `api/PublicTypes.Bezoro.ECS.SourceGen.txt`
- Verify: XML documentation on every public member changed in Tasks 2–6.

**Interfaces:**
- Consumes: final compiled public signatures and behavior.
- Produces: truthful canonical guidance, explicit compatibility notes, and checked API baselines.

- [ ] **Step 1: Update the ECS README to canonical vocabulary**

Make these statements explicit:

```text
Read/TryRead are copy/readonly component access.
Write/TryWrite are mutable component access.
ReadResource/TryReadResource/WriteResource are the resource access vocabulary.
WorldConfig is canonical; WorldOptions is compatibility-only and ChunkSizeInBytes is ignored.
CommandStream and SystemContext.CommandStream are canonical deferred mutation surfaces.
QueryView<TSpec> is the canonical fluent ForEach/Run/RunParallel surface.
QueryCursor and compiled handles remain supported low-level hot-path APIs.
World.ForEach remains supported because QueryView has no exact entity-less delegate replacement.
World Run/RunEntity/RunParallel/RunParallelEntity families are compatibility forwarders.
Managed QueryView traversal is direct chunk traversal; ForEachRead does not mark changes.
```

Replace examples using `Get`, `GetResource`, `CommandBuffer`, `SystemContext.Commands`, or direct World job methods with canonical examples. Add a compatibility table with every exact obsolete message from this plan.

- [ ] **Step 2: Update SourceGen and test documentation**

In SourceGen README, state that `Get`/`GetResource` compatibility aliases infer writes and `TryGetManaged` infers a read; generated World job extensions validate the supplied handle and route through QueryView. Retain documentation for dormant recognizers.

In the tests README, document compatibility-attribute/forwarder coverage, managed direct traversal/read-only tracking, parallel stable-handle coverage, and scalar overwrite characterization.

- [ ] **Step 3: Review all changed public XML docs**

Run:

```powershell
rg -n 'public (sealed class WorldOptions|readonly struct CommandBuffer|readonly struct SystemContext|ref .* Get<|ref .* GetResource<|TryGetManaged|CreateCommandBuffer|BeginCommands|void Run)' src/Bezoro.ECS
```

Expected: each changed public declaration has a truthful summary, type-parameter/parameter/return/exception tags where applicable, and compatibility language that points to the exact canonical API.

- [ ] **Step 4: Check public type baselines**

Run:

```powershell
./scripts/Export-PublicApi.ps1 -Check
git diff -- api/PublicTypes.Bezoro.ECS.txt api/PublicTypes.Bezoro.ECS.SourceGen.txt
```

Expected: check passes and type baselines do not change because no public type is added or removed. If the script reports a real signature-relevant baseline delta, use its documented update mode, inspect the exact diff, and rerun `-Check`; never hand-edit a generated baseline.

- [ ] **Step 5: Commit after explicit authorization only**

```powershell
git add src/Bezoro.ECS/README.md src/Bezoro.ECS.SourceGen/README.md tests/Bezoro.ECS.Tests/README.md benchmarks/Bezoro.ECS.Benchmarks/README.md api
git commit -m "Document canonical ECS APIs"
```

Expected: documentation and any script-generated intentional baseline evidence only. Skip without explicit authorization.

---

### Task 12: Capture after benchmarks and run final ECS/repository gates

**Files:**
- Verify: all files changed by Tasks 1–11.
- Verify: `bezoro.framework.sln`
- Verify: `BenchmarkDotNet.Artifacts/results/Bezoro.ECS.Benchmarks.EcsWorldQueryViewBenchmarks-report-github.md`
- Verify: `BenchmarkDotNet.Artifacts/results/Bezoro.ECS.Benchmarks.EcsWorldHotPathBenchmarks-report-github.md`
- Verify: `BenchmarkDotNet.Artifacts/results/Bezoro.ECS.Benchmarks.EcsWorldComponentAccessBenchmarks-report-github.md`

**Interfaces:**
- Consumes: completed ECS implementation, tests, docs, and before measurements.
- Produces: fresh evidence for behavior, compatibility, multi-target compilation, public API stability, structural simplification, and performance.

- [ ] **Step 1: Inspect the complete diff and policy scans**

```powershell
git status --short
git diff --stat
git diff --check
rg -n 'IDirectEntityChunkExecutor|ExecuteDirectEntityValidated|MoveEntityToArchetypeWithSetKnownTransition' src/Bezoro.ECS
rg -n 'MaterializeQueryEntities|EntityStartIndex' src/Bezoro.ECS/Internal/WorldDirectIterationService.cs src/Bezoro.ECS/Types/QueryView.cs
rg -n 'RuntimeHelpers|TypeTraits<' src/Bezoro.ECS/Types/QueryView.cs
```

Expected: only in-scope files changed; whitespace check passes; removed duplicate/materialization symbols have no matches. `MaterializeQueryEntities` may remain elsewhere for cursor/dormant capabilities.

- [ ] **Step 2: Run full ECS and dependent tests**

```powershell
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj --no-restore --verbosity minimal
dotnet test tests/Bezoro.GameSystems.Tests/Bezoro.GameSystems.Tests.csproj --no-restore --verbosity minimal
dotnet test bezoro.framework.sln --no-restore --verbosity minimal
```

Expected: all tests pass with zero failures and zero warnings.

- [ ] **Step 3: Build every configuration/target**

```powershell
dotnet build src/Bezoro.ECS/Bezoro.ECS.csproj --no-restore --verbosity minimal
dotnet build src/Bezoro.ECS.SourceGen/Bezoro.ECS.SourceGen.csproj --no-restore --verbosity minimal
dotnet build bezoro.framework.sln --no-restore --verbosity minimal
dotnet build bezoro.framework.sln -c Release --no-restore --verbosity minimal
```

Expected: ECS compiles `net9.0` and `netstandard2.1`, SourceGen compiles `netstandard2.0`, full Debug/Release solution builds report zero warnings/errors.

- [ ] **Step 4: Recheck API baselines**

```powershell
./scripts/Export-PublicApi.ps1 -Check
```

Expected: pass.

- [ ] **Step 5: Capture reliable after benchmarks**

```powershell
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj -- --reliable --filter "*EcsWorldQueryViewBenchmarks*"
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj -- --reliable --filter "*EcsWorldHotPathBenchmarks*"
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj -- --reliable --filter "*EcsWorldComponentAccessBenchmarks*"
```

Expected: successful reports. Compare each after mean/allocation with Task 1. Managed mutable/read-only and parallel entity-aware cases must show no material unexplained regression; sequential direct/cursor paths must remain allocation-free after warm-up. Any >5% mean regression or new allocation requires diagnosis, a rerun to exclude noise, and explicit review before completion.

- [ ] **Step 6: Audit compatibility and dormant capability preservation**

```powershell
rg -n '\[Obsolete\(' src/Bezoro.ECS
rg -n 'LegacyQuery|ForEachRw|ForEachRW|QueryCursor|MaterializeQueryEntities' src/Bezoro.ECS src/Bezoro.ECS.SourceGen
```

Expected: every approved alias has the exact guidance in this plan; retained cursor, recognizer, and materialization capability still exists where required outside the simplified paths.

- [ ] **Step 7: Perform final self-review**

Compare the diff and evidence against every Workstream 4 bullet in `docs/superpowers/specs/2026-07-19-repository-complexity-simplification-design.md`. Then run:

```powershell
$tokens = @('T' + 'BD', 'T' + 'ODO', 'FIX' + 'ME', 'X' + 'XX', 'similar' + ' to')
$tokens | ForEach-Object { Select-String -Path docs/superpowers/plans/2026-07-19-ecs-complexity-simplification.md -Pattern $_ }
```

Expected: every required API, internal consolidation, test, doc, target, and benchmark item has direct evidence; the scan returns no matches. Existing actionable source markers are not removed by this work.

- [ ] **Step 8: Commit final verification adjustments after explicit authorization only**

```powershell
git add src/Bezoro.ECS src/Bezoro.ECS.SourceGen src/Bezoro.GameSystems tests/Bezoro.ECS.Tests tests/Bezoro.GameSystems.Tests benchmarks/Bezoro.ECS.Benchmarks docs api
git commit -m "Complete ECS complexity simplification"
```

Expected: only any final reviewed adjustments not already committed. Skip without current explicit authorization; never commit generated BenchmarkDotNet artifacts.
