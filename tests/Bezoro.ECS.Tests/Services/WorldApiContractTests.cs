using System;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(World))]
public class WorldApiContractTests
{
	[Fact]
	public void Add_WhenCalledWithoutValue_ShouldAddDefaultInitializedComponent()
	{
		var world  = new World();
		var entity = world.Spawn();

		world.Add<ApiHealth>(entity);

		world.Has<ApiHealth>(entity).Should().BeTrue();
		world.Get<ApiHealth>(entity).Should().Be(default(ApiHealth));
	}

	[Fact]
	public void SpawnOverloads_WhenCalled_ShouldInitializeComponents()
	{
		var world = new World();

		var entity = world.Spawn(
			new ApiPosition { X = 3f, Y = 4f },
			new ApiVelocity { X = 1f, Y = -2f },
			new ApiHealth { Current = 7, Max = 10 }
		);

		world.Get<ApiPosition>(entity).Should().Be(new ApiPosition { X = 3f, Y = 4f });
		world.Get<ApiVelocity>(entity).Should().Be(new ApiVelocity { X = 1f, Y = -2f });
		world.Get<ApiHealth>(entity).Should().Be(new ApiHealth { Current = 7, Max = 10 });
	}

	[Fact]
	public void Resources_WhenSet_ShouldBeReadableByReference()
	{
		var world = new World();
		world.SetResource(new ApiTuning { Gravity = 9.81f });

		ref var tuning = ref world.GetResource<ApiTuning>();
		tuning.Gravity = 12.5f;

		world.GetResource<ApiTuning>().Gravity.Should().Be(12.5f);
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
	public void CommandStreamPlayback_WhenCreatingAndSettingComponent_ShouldApplyChanges()
	{
		var world  = new World();
		var stream = world.CreateCommandStream();
		var entity = stream.CreateEntity();
		stream.Set(entity, new ApiPosition { X = 11f, Y = 13f });

		world.Playback(stream);

		world.EntityCount.Should().Be(1);
		var handle = world.Compile<PositionQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(1);

		var created = cursor.Current[0];
		world.Get<ApiPosition>(created).Should().Be(new ApiPosition { X = 11f, Y = 13f });
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
		IWorld world = new World();
		var entity = world.Spawn();
		world.Add(entity, new ApiPosition { X = 5f, Y = 8f });

		world.Has<ApiPosition>(entity).Should().BeTrue();
		world.TryGet(entity, out ApiPosition position).Should().BeTrue();
		position.Should().Be(new ApiPosition { X = 5f, Y = 8f });
	}

	private readonly struct PositionQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.All<ApiPosition>();
	}
}

internal struct ApiPosition
{
	public float X;
	public float Y;
}

internal struct ApiVelocity
{
	public float X;
	public float Y;
}

internal struct ApiHealth
{
	public int Current;
	public int Max;
}

internal struct ApiTuning
{
	public float Gravity;
}
