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

	#pragma warning disable CS0618

	[Theory]
	[InlineData(0, 1)]
	[InlineData(-1, 64 * 1024)]
	public void WorldOptions_WhenChunkCapacityIsNonPositive_ShouldUseWorldConfigDefaultAndIgnoreChunkSize(
		int chunkCapacity,
		int chunkSizeInBytes)
	{
		using var world = new World(
			new WorldOptions { ChunkCapacity = chunkCapacity, ChunkSizeInBytes = chunkSizeInBytes }
		);
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
		using var world = new World(new WorldOptions { ChunkCapacity = 1, ChunkSizeInBytes = 64 * 1024 });
		world.Spawn(new ApiPosition());
		world.Spawn(new ApiPosition());

		var handle = world.Compile<PositionQuerySpec>();

		world.GetQueryDiagnostics(handle).MatchingChunkCount.Should().Be(2);
	}

	[Fact]
	public void WorldOptions_WhenMaxDegreeOfParallelismIsInvalid_ShouldPreserveValidation()
	{
		var act = () => new World(new WorldOptions { MaxDegreeOfParallelism = 0 });

		act.Should().Throw<ArgumentOutOfRangeException>()
		   .Which.ParamName.Should().Be(nameof(WorldConfig.MaxDegreeOfParallelism));
	}

	[Fact]
	public void WorldOptions_WhenNull_ShouldPreserveNullValidation()
	{
		var act = () => new World((WorldOptions)null!);

		act.Should().Throw<ArgumentNullException>()
		   .Which.ParamName.Should().Be("options");
	}

	[Fact]
	public void WorldOptions_WhenOtherCapacitiesAreUnspecified_ShouldUseWorldConfigDefaults()
	{
		var defaults = new WorldConfig();
		using var world = new World(new WorldOptions());
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
		var typeAttribute = typeof(WorldOptions).GetCustomAttribute<ObsoleteAttribute>();
		var constructor = typeof(World).GetConstructor([typeof(WorldOptions)]);

		typeAttribute.Should().NotBeNull();
		typeAttribute!.Message.Should().Be(WorldOptionsObsoleteMessage);
		typeAttribute.IsError.Should().BeFalse();
		constructor.Should().NotBeNull();

		var constructorAttribute = constructor!.GetCustomAttribute<ObsoleteAttribute>();
		constructorAttribute.Should().NotBeNull();
		constructorAttribute!.Message.Should().Be(WorldOptionsConstructorObsoleteMessage);
		constructorAttribute.IsError.Should().BeFalse();
	}

	#pragma warning restore CS0618

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
	public void PublicSystemSurface_WhenInspectingContracts_ShouldExposeCommandBufferOnSystemContext()
	{
		typeof(SystemContext)
			.GetProperty(nameof(SystemContext.Commands))!
			.PropertyType
			.Should()
			.Be(typeof(CommandBuffer));
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
