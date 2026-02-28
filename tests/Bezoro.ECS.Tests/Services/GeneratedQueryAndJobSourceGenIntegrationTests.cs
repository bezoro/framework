using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(World))]
public class GeneratedQueryAndJobSourceGenIntegrationTests
{
	[Fact]
	public void Compile_WhenUsingGeneratedAddedFilter_ShouldReturnOnlyNewAdditions()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 64,
				CommandPayloadCapacityPerType = 64,
				QueryResultCapacity           = 16
			}
		);

		var first  = world.Spawn(new GeneratedPosition { X = 1, Y = 1 });
		var second = world.Spawn(new GeneratedVelocity { X = 9, Y = 9 });

		var handle = world.Compile<GeneratedAddedPositionQuery>();
		using (var initial = world.Execute(handle))
		{
			initial.MoveNext().Should().BeTrue();
			initial.Current.Length.Should().Be(1);
			initial.Current[0].Should().Be(first);
		}

		using (var unchanged = world.Execute(handle))
		{
			unchanged.MoveNext().Should().BeTrue();
			unchanged.Current.Length.Should().Be(0);
		}

		world.Set(first, new GeneratedPosition { X  = 10, Y = 10 });
		world.Add(second, new GeneratedPosition { X = 20, Y = 20 });
		using var added = world.Execute(handle);
		added.MoveNext().Should().BeTrue();
		added.Current.Length.Should().Be(1);
		added.Current[0].Should().Be(second);
	}

	[Fact]
	public void Compile_WhenUsingGeneratedChangedFilter_ShouldReturnOnlyRecentlyChangedEntities()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 64,
				CommandPayloadCapacityPerType = 64,
				QueryResultCapacity           = 16
			}
		);

		var first = world.Spawn(new GeneratedPosition { X = 1, Y = 1 });
		_ = world.Spawn(new GeneratedPosition { X = 2, Y = 2 });

		var handle = world.Compile<GeneratedChangedPositionQuery>();
		using (var initial = world.Execute(handle))
		{
			initial.MoveNext().Should().BeTrue();
			initial.Current.Length.Should().Be(2);
		}

		using (var unchanged = world.Execute(handle))
		{
			unchanged.MoveNext().Should().BeTrue();
			unchanged.Current.Length.Should().Be(0);
		}

		world.Set(first, new GeneratedPosition { X = 10, Y = 10 });
		using var changed = world.Execute(handle);
		changed.MoveNext().Should().BeTrue();
		changed.Current.Length.Should().Be(1);
		changed.Current[0].Should().Be(first);
	}

	[Fact]
	public void Compile_WhenUsingGeneratedOptionalFilter_ShouldIncludeEntitiesWithAndWithoutOptionalType()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 64,
				CommandPayloadCapacityPerType = 64,
				QueryResultCapacity           = 16
			}
		);

		var first = world.Spawn(new GeneratedPosition { X = 1, Y = 1 });
		var second = world.Spawn(
			new GeneratedPosition { X = 2, Y = 2 },
			new GeneratedVelocity { X = 3, Y = 3 }
		);

		_ = world.Spawn(new GeneratedVelocity { X = 9, Y = 9 });

		var       handle = world.Compile<GeneratedPositionOptionalVelocityQuery>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(2);
		var entities = cursor.Current.ToArray();
		entities.Should().Contain(first);
		entities.Should().Contain(second);
	}

	[Fact]
	public void Compile_WhenUsingGeneratedQuerySpecFromAttributes_ShouldMatchExpectedEntities()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 64,
				CommandPayloadCapacityPerType = 64,
				QueryResultCapacity           = 16
			}
		);

		var first = world.Spawn(
			new GeneratedPosition { X = 1, Y = 1 },
			new GeneratedVelocity { X = 2, Y = 2 }
		);

		var second = world.Spawn(
			new GeneratedPosition { X     = 3, Y = 3 },
			new GeneratedAcceleration { X = 5, Y = 5 }
		);

		_ = world.Spawn(
			new GeneratedPosition { X = 7, Y = 7 },
			new GeneratedVelocity { X = 1, Y = 1 },
			new GeneratedFrozen()
		);

		_ = world.Spawn(new GeneratedVelocity { X = 8, Y = 8 });

		var       handle = world.Compile<GeneratedPositionMotionQuery>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(2);
		var entities = cursor.Current.ToArray();
		entities.Should().Contain(first);
		entities.Should().Contain(second);
	}

	[Fact]
	public void Run_WhenUsingGeneratedCursorJobExtension_ShouldMutateCurrentChunk()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 8,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 32,
				CommandPayloadCapacityPerType = 32,
				QueryResultCapacity           = 8
			}
		);

		var entity = world.Spawn(
			new GeneratedPosition { X = 10, Y = -2 },
			new GeneratedVelocity { X = 1, Y  = 4 }
		);

		var       handle = world.Compile<GeneratedPositionVelocityQuery>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.Run(new GeneratedIntegrateJob(3f));

		world.Get<GeneratedPosition>(entity).Should().Be(new GeneratedPosition { X = 13, Y = 10 });
	}

	[Fact]
	public void Run_WhenUsingGeneratedWorldJobExtension_ShouldMutateMatchingEntities()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 8,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 32,
				CommandPayloadCapacityPerType = 32,
				QueryResultCapacity           = 8
			}
		);

		var entity = world.Spawn(
			new GeneratedPosition { X = 1, Y = 2 },
			new GeneratedVelocity { X = 3, Y = -1 }
		);

		var handle = world.Compile<GeneratedPositionVelocityQuery>();
		world.Run(handle, new GeneratedIntegrateJob(2f));

		world.Get<GeneratedPosition>(entity).Should().Be(new GeneratedPosition { X = 7, Y = 0 });
	}

	[Fact]
	public void Run_WhenUsingGeneratedQueryViewJobExtension_ShouldMutateMatchingEntities()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 8,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 32,
				CommandPayloadCapacityPerType = 32,
				QueryResultCapacity           = 8
			}
		);

		var entity = world.Spawn(
			new GeneratedPosition { X = 2, Y = 3 },
			new GeneratedVelocity { X = -1, Y = 5 }
		);

		world.Query<GeneratedPositionVelocityQuery>().Run(new GeneratedIntegrateJob(4f));

		world.Get<GeneratedPosition>(entity).Should().Be(new GeneratedPosition { X = -2, Y = 23 });
	}

	[Fact]
	public void RunParallel_WhenUsingGeneratedQueryViewJobExtension_ShouldMutateMatchingEntities()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 32,
				CommandPayloadCapacityPerType = 32,
				QueryResultCapacity           = 16,
				MaxDegreeOfParallelism        = 4
			}
		);

		var first = world.Spawn(
			new GeneratedPosition { X = 1, Y = 1 },
			new GeneratedVelocity { X = 2, Y = 3 }
		);

		var second = world.Spawn(
			new GeneratedPosition { X = 10, Y = 20 },
			new GeneratedVelocity { X = -4, Y = 1 }
		);

		world.Query<GeneratedPositionVelocityQuery>().RunParallel(new GeneratedIntegrateJob(2f), 2);

		world.Get<GeneratedPosition>(first).Should().Be(new GeneratedPosition { X = 5, Y = 7 });
		world.Get<GeneratedPosition>(second).Should().Be(new GeneratedPosition { X = 2, Y = 22 });
	}

	[Fact]
	public void Run_WhenUsingGeneratedQueryViewEntityJobExtension_ShouldReceiveEntityAndMutateMatchingEntities()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 8,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 32,
				CommandPayloadCapacityPerType = 32,
				QueryResultCapacity           = 8
			}
		);

		var entity = world.Spawn(
			new GeneratedPosition { X = 5, Y = 6 },
			new GeneratedVelocity { X = 1, Y = 2 }
		);

		GeneratedEntityIntegrateJob.LastEntity = Entity.None;
		world.Query<GeneratedPositionVelocityQuery>().Run(new GeneratedEntityIntegrateJob(3f));

		GeneratedEntityIntegrateJob.LastEntity.Should().Be(entity);
		world.Get<GeneratedPosition>(entity).Should().Be(new GeneratedPosition { X = 8, Y = 12 });
	}

	[Fact]
	public void RunParallel_WhenUsingGeneratedQueryViewEntityJobExtension_ShouldMutateMatchingEntities()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 32,
				CommandPayloadCapacityPerType = 32,
				QueryResultCapacity           = 16,
				MaxDegreeOfParallelism        = 4
			}
		);

		var first = world.Spawn(
			new GeneratedPosition { X = 2, Y = 4 },
			new GeneratedVelocity { X = 1, Y = 1 }
		);

		var second = world.Spawn(
			new GeneratedPosition { X = 9, Y = 3 },
			new GeneratedVelocity { X = -2, Y = 5 }
		);

		world.Query<GeneratedPositionVelocityQuery>().RunParallel(new GeneratedEntityIntegrateJob(2f), 2);

		world.Get<GeneratedPosition>(first).Should().Be(new GeneratedPosition { X = 4, Y = 6 });
		world.Get<GeneratedPosition>(second).Should().Be(new GeneratedPosition { X = 5, Y = 13 });
	}
}

internal struct GeneratedAcceleration
{
	public float X;
	public float Y;
}

[Query]
[Added<GeneratedPosition>]
internal readonly partial struct GeneratedAddedPositionQuery;

[Query]
[Changed<GeneratedPosition>]
internal readonly partial struct GeneratedChangedPositionQuery;

internal struct GeneratedFrozen;

internal readonly struct GeneratedIntegrateJob(float dt) : IForEach<GeneratedPosition, GeneratedVelocity>
{
	public void Execute(ref GeneratedPosition component1, in GeneratedVelocity component2)
	{
		component1.X += component2.X * dt;
		component1.Y += component2.Y * dt;
	}
}

internal struct GeneratedEntityIntegrateJob(float dt) : IForEachEntity<GeneratedPosition, GeneratedVelocity>
{
	public static Entity LastEntity;

	public void Execute(Entity entity, ref GeneratedPosition component1, in GeneratedVelocity component2)
	{
		LastEntity = entity;
		component1.X += component2.X * dt;
		component1.Y += component2.Y * dt;
	}
}

internal struct GeneratedPosition
{
	public float X;
	public float Y;
}

[Query]
[All<GeneratedPosition>]
[Any<GeneratedVelocity, GeneratedAcceleration>]
[None<GeneratedFrozen>]
internal readonly partial struct GeneratedPositionMotionQuery;

[Query]
[All<GeneratedPosition>]
[Optional<GeneratedVelocity>]
internal readonly partial struct GeneratedPositionOptionalVelocityQuery;

[Query]
[All<GeneratedPosition>]
[All<GeneratedVelocity>]
internal readonly partial struct GeneratedPositionVelocityQuery;

internal struct GeneratedVelocity
{
	public float X;
	public float Y;
}
