using System;
using System.Linq;
using System.Reflection;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Options;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(World))]
public class WorldApiContractTests
{
	private const string GetObsoleteMessage =
		"Use Read<T>(Entity) for read-only access or Write<T>(Entity) for mutable access instead.";
	private const string GetResourceObsoleteMessage =
		"Use ReadResource<T>() for read-only access or WriteResource<T>() for mutable access instead.";
	private const string TryGetManagedObsoleteMessage = "Use TryRead<T>(Entity, out T) instead.";
	private const string WorldOptionsObsoleteMessage = "Use WorldConfig instead.";
	private const string WorldOptionsConstructorObsoleteMessage = "Use World(WorldConfig) instead.";
	private const string CommandBufferObsoleteMessage = "Use CommandStream instead.";
	private const string SystemContextCommandsObsoleteMessage = "Use SystemContext.CommandStream instead.";
	private const string SystemContextCommandBufferConstructorObsoleteMessage =
		"Use SystemContext(float, Stage, World, CommandStream) instead.";
	private const string WorldCommandAliasObsoleteMessage = "Use CreateCommandStream() instead.";
	private const string WorldRunObsoleteMessage = "Use QueryView<TSpec>.Run(job) instead.";
	private const string WorldRunEntityObsoleteMessage = "Use QueryView<TSpec>.RunEntity(job) instead.";
	private const string WorldRunParallelObsoleteMessage =
		"Use QueryView<TSpec>.RunParallel(job, degreeOfParallelism) instead.";
	private const string WorldRunParallelEntityObsoleteMessage =
		"Use QueryView<TSpec>.RunParallelEntity(job, degreeOfParallelism) instead.";

	[Theory]
	[InlineData(0, 1)]
	[InlineData(-1, 64 * 1024)]
	public void WorldOptions_WhenChunkCapacityIsNonPositive_ShouldUseWorldConfigDefaultAndIgnoreChunkSize(
		int chunkCapacity,
		int chunkSizeInBytes)
	{
		#pragma warning disable CS0618
		using var world = new World(
			new WorldOptions { ChunkCapacity = chunkCapacity, ChunkSizeInBytes = chunkSizeInBytes }
		);
		#pragma warning restore CS0618
		for (var i = 0; i < 256; i++)
			world.Spawn(new ApiPosition());

		var handle = world.Compile<PositionQuerySpec>();
		world.GetQueryDiagnostics(handle).MatchingChunkCount.Should().Be(1);

		world.Spawn(new ApiPosition());

		world.GetQueryDiagnostics(handle).MatchingChunkCount.Should().Be(2);
	}

	[Fact]
	public void WorldOptions_WhenChunkCapacityIsPositive_ShouldMapCapacityExactlyAndIgnoreChunkSize()
	{
		#pragma warning disable CS0618
		using var world = new World(new WorldOptions { ChunkCapacity = 1, ChunkSizeInBytes = 64 * 1024 });
		#pragma warning restore CS0618
		world.Spawn(new ApiPosition());
		world.Spawn(new ApiPosition());

		var handle = world.Compile<PositionQuerySpec>();

		world.GetQueryDiagnostics(handle).MatchingChunkCount.Should().Be(2);
	}

	[Fact]
	public void WorldOptions_WhenMaxDegreeOfParallelismIsInvalid_ShouldPreserveValidation()
	{
		#pragma warning disable CS0618
		var act = () => new World(new WorldOptions { MaxDegreeOfParallelism = 0 });
		#pragma warning restore CS0618

		act.Should().Throw<ArgumentOutOfRangeException>()
		   .Which.ParamName.Should().Be(nameof(WorldConfig.MaxDegreeOfParallelism));
	}

	[Fact]
	public void WorldOptions_WhenNull_ShouldPreserveNullValidation()
	{
		#pragma warning disable CS0618
		var act = () => new World((WorldOptions)null!);
		#pragma warning restore CS0618

		act.Should().Throw<ArgumentNullException>()
		   .Which.ParamName.Should().Be("options");
	}

	[Fact]
	public void WorldOptions_WhenOtherCapacitiesAreUnspecified_ShouldUseWorldConfigDefaults()
	{
		var defaults = new WorldConfig();
		#pragma warning disable CS0618
		using var world = new World(new WorldOptions());
		#pragma warning restore CS0618
		using var stream = world.CreateCommandStream();

		var worldDiagnostics = world.GetDiagnostics();
		var streamDiagnostics = stream.GetDiagnostics();

		worldDiagnostics.EntityArena.Capacity.Should().Be(defaults.EntityCapacity);
		worldDiagnostics.ComponentTypeArena.Capacity.Should().Be(defaults.ComponentTypeCapacity);
		worldDiagnostics.QueryResultArena.Capacity.Should().Be(defaults.QueryResultCapacity);
		streamDiagnostics.CommandCapacity.Should().Be(defaults.CommandCapacity);
	}

	[Fact]
	public void WorldOptions_WhenInspected_ShouldHaveExactNonErrorObsoleteAttributes()
	{
		#pragma warning disable CS0618
		var typeAttribute = typeof(WorldOptions).GetCustomAttribute<ObsoleteAttribute>();
		var constructor = typeof(World).GetConstructor([typeof(WorldOptions)]);
		#pragma warning restore CS0618

		typeAttribute.Should().NotBeNull();
		typeAttribute!.Message.Should().Be(WorldOptionsObsoleteMessage);
		typeAttribute.IsError.Should().BeFalse();
		constructor.Should().NotBeNull();

		var constructorAttribute = constructor!.GetCustomAttribute<ObsoleteAttribute>();
		constructorAttribute.Should().NotBeNull();
		constructorAttribute!.Message.Should().Be(WorldOptionsConstructorObsoleteMessage);
		constructorAttribute.IsError.Should().BeFalse();
	}

	[Fact]
	public void Add_WhenCalledWithoutValue_ShouldAddDefaultInitializedComponent()
	{
		using var world  = new World();
		var       entity = world.Spawn();

		world.Add<ApiHealth>(entity);

		world.Has<ApiHealth>(entity).Should().BeTrue();
		world.Read<ApiHealth>(entity).Should().Be(default(ApiHealth));
	}

	[Fact]
	public void CommandStreamPlayback_WhenCreatingAndSettingComponent_ShouldApplyChanges()
	{
		using var world  = new World();
		var       stream = world.CreateCommandStream();
		var       entity = stream.CreateEntity();
		stream.Set(entity, new ApiPosition { X = 11f, Y = 13f });

		world.Playback(stream);

		world.EntityCount.Should().Be(1);
		var       handle = world.Compile<PositionQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(1);

		var created = cursor.Current[0];
		world.Read<ApiPosition>(created).Should().Be(new ApiPosition { X = 11f, Y = 13f });
	}

	[Fact]
	public void Execute_WhenUsingHandleFromDifferentWorld_ShouldThrowInvalidOperationException()
	{
		var owner  = new World();
		var other  = new World();
		var handle = owner.Compile<PositionQuerySpec>();

		var act = () => other.Execute(handle);

		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("*different world*");
	}

	[Fact]
	public void IWorldSurface_WhenUsedThroughInterface_ShouldSupportCoreOperations()
	{
		IWorld world  = new World();
		var    entity = world.Spawn();
		world.Add(entity, new ApiPosition { X = 5f, Y = 8f });

		world.Has<ApiPosition>(entity).Should().BeTrue();
		world.TryGet(entity, out ApiPosition position).Should().BeTrue();
		position.Should().Be(new ApiPosition { X = 5f, Y = 8f });
	}

	[Theory]
	[InlineData(typeof(World), nameof(World.Get), GetObsoleteMessage)]
	[InlineData(typeof(IWorld), nameof(IWorld.Get), GetObsoleteMessage)]
	[InlineData(typeof(World), nameof(World.GetResource), GetResourceObsoleteMessage)]
	[InlineData(typeof(IWorld), nameof(IWorld.GetResource), GetResourceObsoleteMessage)]
	[InlineData(typeof(World), nameof(World.TryGetManaged), TryGetManagedObsoleteMessage)]
	public void LegacyAccessMember_WhenInspected_ShouldHaveExactNonErrorObsoleteAttribute(
		Type   declaringType,
		string memberName,
		string expectedMessage)
	{
		var method = declaringType
			.GetMethods()
			.Single(candidate => candidate.Name == memberName && candidate.IsGenericMethodDefinition);

		var attribute = method.GetCustomAttribute<ObsoleteAttribute>();

		attribute.Should().NotBeNull();
		attribute!.Message.Should().Be(expectedMessage);
		attribute.IsError.Should().BeFalse();
	}

	[Theory]
	[InlineData(nameof(World.Run), WorldRunObsoleteMessage)]
	[InlineData(nameof(World.RunEntity), WorldRunEntityObsoleteMessage)]
	[InlineData(nameof(World.RunParallel), WorldRunParallelObsoleteMessage)]
	[InlineData(nameof(World.RunParallelEntity), WorldRunParallelEntityObsoleteMessage)]
	public void LegacyWorldJobMember_WhenInspected_ShouldHaveExactNonErrorObsoleteAttributes(
		string memberName,
		string expectedMessage)
	{
		var methods = typeof(World)
			.GetMethods()
			.Where(candidate => candidate.Name == memberName && candidate.IsGenericMethodDefinition)
			.ToArray();

		methods.Should().HaveCount(4);
		foreach (var method in methods)
		{
			var attribute = method.GetCustomAttribute<ObsoleteAttribute>();

			attribute.Should().NotBeNull();
			attribute!.Message.Should().Be(expectedMessage);
			attribute.IsError.Should().BeFalse();
		}
	}

	[Fact]
	public void WorldForEachMembers_WhenInspected_ShouldRemainNonObsolete()
	{
		var methods = typeof(World)
			.GetMethods()
			.Where(candidate => candidate.Name == nameof(World.ForEach) && candidate.IsGenericMethodDefinition)
			.ToArray();

		methods.Should().HaveCount(4);
		methods.Should().OnlyContain(method => method.GetCustomAttribute<ObsoleteAttribute>() == null);
	}

	[Fact]
	public void LegacyWorldRun_WhenCalled_ShouldPreserveMutation()
	{
		using var world = new World();
		var entity = world.Spawn(new ApiPosition { X = 3f });
		var handle = world.Compile<PositionQuerySpec>();

		#pragma warning disable CS0618
		world.Run<PositionQuerySpec, ApiIncrementJob, ApiPosition>(handle, new ApiIncrementJob());
		#pragma warning restore CS0618

		world.Read<ApiPosition>(entity).X.Should().Be(4f);
	}

	[Fact]
	public void LegacyWorldRunEntity_WhenCalled_ShouldPreserveEntityDelivery()
	{
		using var world = new World();
		var entity = world.Spawn(new ApiPosition());
		var handle = world.Compile<PositionQuerySpec>();

		#pragma warning disable CS0618
		world.RunEntity<PositionQuerySpec, ApiEntityCaptureJob, ApiPosition>(handle, new ApiEntityCaptureJob());
		#pragma warning restore CS0618

		world.Read<ApiPosition>(entity).Should().Be(new ApiPosition { X = entity.Id, Y = entity.Version });
	}

	[Fact]
	public void LegacyWorldRunParallel_WhenCalled_ShouldPreserveMutation()
	{
		using var world = new World();
		var entity = world.Spawn(new ApiPosition { X = 3f });
		var handle = world.Compile<PositionQuerySpec>();

		#pragma warning disable CS0618
		world.RunParallel<PositionQuerySpec, ApiIncrementJob, ApiPosition>(handle, new ApiIncrementJob(), 1);
		#pragma warning restore CS0618

		world.Read<ApiPosition>(entity).X.Should().Be(4f);
	}

	[Fact]
	public void LegacyWorldRunParallelEntity_WhenCalled_ShouldPreserveEntityDelivery()
	{
		using var world = new World();
		var entity = world.Spawn(new ApiPosition());
		var handle = world.Compile<PositionQuerySpec>();

		#pragma warning disable CS0618
		world.RunParallelEntity<PositionQuerySpec, ApiEntityCaptureJob, ApiPosition>(
			handle,
			new ApiEntityCaptureJob(),
			1
		);
		#pragma warning restore CS0618

		world.Read<ApiPosition>(entity).Should().Be(new ApiPosition { X = entity.Id, Y = entity.Version });
	}

	[Fact]
	public void PublicQuerySurface_WhenInspectingContracts_ShouldNotExposeIgnoredParameterizedQueryOverload()
	{
		var interfaceMethods = typeof(IWorld)
							  .GetMethods()
							  .Where(static method => method.Name == nameof(IWorld.Query))
							  .ToArray();
		var worldMethods = typeof(World)
						  .GetMethods()
						  .Where(static method => method.Name == nameof(World.Query))
						  .ToArray();

		interfaceMethods.Should().ContainSingle(static method => method.GetParameters().Length == 0);
		interfaceMethods.Should().NotContain(static method => method.GetParameters().Length == 1);

		worldMethods.Should().ContainSingle(
			static method => method.IsGenericMethodDefinition && method.GetParameters().Length == 0
		);
		worldMethods.Should().NotContain(
			static method => method.IsGenericMethodDefinition && method.GetParameters().Length == 1
		);
	}

	[Fact]
	public void PublicQuerySurface_WhenInspectingContracts_ShouldExposeQueryViewFromIWorldAndWorld()
	{
		var interfaceMethod = typeof(IWorld)
							 .GetMethods()
							 .Single(static method => method.Name == nameof(IWorld.Query) && method.GetParameters().Length == 0);
		var worldMethod = typeof(World)
						 .GetMethods()
						 .Single(
							  static method => method.Name == nameof(World.Query)
							                && method.IsGenericMethodDefinition
							                && method.GetParameters().Length == 0
						  );

		interfaceMethod.ReturnType.GetGenericTypeDefinition().Should().Be(typeof(QueryView<>));
		worldMethod.ReturnType.GetGenericTypeDefinition().Should().Be(typeof(QueryView<>));
	}

	[Fact]
	public void SystemContext_WhenConstructedWithCommandBuffer_ShouldPreserveUnderlyingStreamIdentity()
	{
		using var world = new World();
		using var stream = world.CreateCommandStream();
		#pragma warning disable CS0618
		var buffer = new CommandBuffer(stream);

		var context = new SystemContext(0.25f, Stage.Tick, world, buffer);
		CommandStream unwrapped = context.Commands;
		#pragma warning restore CS0618

		context.CommandStream.Should().BeSameAs(stream);
		unwrapped.Should().BeSameAs(stream);
	}

	[Fact]
	public void LegacyCommandSurface_WhenInspected_ShouldHaveExactNonErrorObsoleteAttributes()
	{
		#pragma warning disable CS0618
		var commandBufferAttribute = typeof(CommandBuffer).GetCustomAttribute<ObsoleteAttribute>();
		var contextConstructor = typeof(SystemContext).GetConstructor(
			[typeof(float), typeof(Stage), typeof(World), typeof(CommandBuffer)]
		);
		var commandsProperty = typeof(SystemContext).GetProperty("Commands");
		#pragma warning restore CS0618
		var createCommandBuffer = typeof(World).GetMethod("CreateCommandBuffer", Type.EmptyTypes);
		var beginCommands = typeof(World).GetMethod("BeginCommands", Type.EmptyTypes);

		commandBufferAttribute.Should().NotBeNull();
		commandBufferAttribute!.Message.Should().Be(CommandBufferObsoleteMessage);
		commandBufferAttribute.IsError.Should().BeFalse();

		contextConstructor.Should().NotBeNull();
		var contextConstructorAttribute = contextConstructor!.GetCustomAttribute<ObsoleteAttribute>();
		contextConstructorAttribute.Should().NotBeNull();
		contextConstructorAttribute!.Message.Should().Be(SystemContextCommandBufferConstructorObsoleteMessage);
		contextConstructorAttribute.IsError.Should().BeFalse();

		commandsProperty.Should().NotBeNull();
		var commandsPropertyAttribute = commandsProperty!.GetCustomAttribute<ObsoleteAttribute>();
		commandsPropertyAttribute.Should().NotBeNull();
		commandsPropertyAttribute!.Message.Should().Be(SystemContextCommandsObsoleteMessage);
		commandsPropertyAttribute.IsError.Should().BeFalse();

		createCommandBuffer.Should().NotBeNull();
		var createCommandBufferAttribute = createCommandBuffer!.GetCustomAttribute<ObsoleteAttribute>();
		createCommandBufferAttribute.Should().NotBeNull();
		createCommandBufferAttribute!.Message.Should().Be(WorldCommandAliasObsoleteMessage);
		createCommandBufferAttribute.IsError.Should().BeFalse();

		beginCommands.Should().NotBeNull();
		var beginCommandsAttribute = beginCommands!.GetCustomAttribute<ObsoleteAttribute>();
		beginCommandsAttribute.Should().NotBeNull();
		beginCommandsAttribute!.Message.Should().Be(WorldCommandAliasObsoleteMessage);
		beginCommandsAttribute.IsError.Should().BeFalse();
	}

	[Fact]
	public void SystemContext_WhenConstructedWithCommandStream_ShouldExposeCanonicalValues()
	{
		using var world = new World();
		using var stream = world.CreateCommandStream();

		var context = new SystemContext(0.25f, Stage.Tick, world, stream);

		context.DeltaTime.Should().Be(0.25f);
		context.Stage.Should().Be(Stage.Tick);
		context.World.Should().BeSameAs(world);
		context.CommandStream.Should().BeSameAs(stream);
	}

	[Fact]
	public void SystemContext_WhenCommandStreamIsNull_ShouldThrowArgumentNullException()
	{
		using var world = new World();

		var act = () => new SystemContext(0.25f, Stage.Tick, world, (CommandStream)null!);

		act.Should().Throw<ArgumentNullException>()
		   .Which.ParamName.Should().Be("commandStream");
	}

	[Fact]
	public void PublicSystemSurface_WhenInspectingContracts_ShouldRetainCommandBufferCompatibilityProperty()
	{
		#pragma warning disable CS0618
		typeof(SystemContext)
			.GetProperty("Commands")!
			.PropertyType
			.Should()
			.Be(typeof(CommandBuffer));
		#pragma warning restore CS0618
	}

	[Fact]
	public void Playback_WhenUsingDifferentWorld_ShouldThrowInvalidOperationException()
	{
		var owner  = new World();
		var other  = new World();
		var stream = owner.CreateCommandStream();

		var act = () => other.Playback(stream);

		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("*different world*");
	}

	[Fact]
	public void RestoreSnapshot_WhenCalledWithoutExplicitAllowLists_ShouldRejectSnapshotTypesByDefault()
	{
		using var world = new World();
		var snapshot = new WorldSnapshot(
			[],
			[
				new(
					new Entity(1, 1),
					[
						new SnapshotComponentRecord(typeof(ApiPosition), new ApiPosition { X = 1f, Y = 2f })
					]
				)
			],
			[]
		);
		var reader = new InMemorySnapshotReader(snapshot);

		var act = () => world.RestoreSnapshot(ref reader);

		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("*not allow-listed*");
	}

	[Fact]
	public void Resources_WhenSet_ShouldBeReadableByReference()
	{
		using var world = new World();
		world.SetResource(new ApiTuning { Gravity = 9.81f });

		#pragma warning disable CS0618
		ref var tuning = ref world.GetResource<ApiTuning>();
		#pragma warning restore CS0618
		tuning.Gravity = 12.5f;

		world.ReadResource<ApiTuning>().Gravity.Should().Be(12.5f);
	}

	[Fact]
	public void SpawnOverloads_WhenCalled_ShouldInitializeComponents()
	{
		using var world = new World();

		var entity = world.Spawn(
			new ApiPosition { X     = 3f, Y  = 4f },
			new ApiVelocity { X     = 1f, Y  = -2f },
			new ApiHealth { Current = 7, Max = 10 }
		);

		world.Read<ApiPosition>(entity).Should().Be(new ApiPosition { X   = 3f, Y  = 4f });
		world.Read<ApiVelocity>(entity).Should().Be(new ApiVelocity { X   = 1f, Y  = -2f });
		world.Read<ApiHealth>(entity).Should().Be(new ApiHealth { Current = 7, Max = 10 });
	}

	private readonly struct PositionQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.All<ApiPosition>();
	}

	private readonly struct ApiIncrementJob : IForEach<ApiPosition>
	{
		public void Execute(ref ApiPosition component1) => component1.X++;
	}

	private readonly struct ApiEntityCaptureJob : IForEachEntity<ApiPosition>
	{
		public void Execute(Entity entity, ref ApiPosition component1)
		{
			component1.X = entity.Id;
			component1.Y = entity.Version;
		}
	}

	private readonly struct InMemorySnapshotReader(WorldSnapshot snapshot) : IWorldSnapshotReader
	{
		public WorldSnapshot Read() => snapshot;
	}
}

internal struct ApiHealth
{
	public int Current;
	public int Max;
}

internal struct ApiPosition
{
	public float X;
	public float Y;
}

internal struct ApiTuning
{
	public float Gravity;
}

internal struct ApiVelocity
{
	public float X;
	public float Y;
}
