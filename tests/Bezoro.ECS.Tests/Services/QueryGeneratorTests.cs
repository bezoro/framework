using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(World))]
public class QueryGeneratorTests
{
	[Fact]
	public void Execute_WhenUsingAllFilters_ShouldMatchExpectedEntities()
	{
		var world = new World();

		var e1 = world.Spawn();
		world.Add(e1, new QueryPosition { X = 1, Y = 1 });
		world.Add(e1, new QueryVelocity { X = 2, Y = 0 });

		var e2 = world.Spawn();
		world.Add(e2, new QueryPosition { X = 1, Y = 1 });

		var handle = world.Compile<PositionVelocityQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();

		cursor.Current.Length.Should().Be(1);
		cursor.Current[0].Should().Be(e1);
	}

	[Fact]
	public void Execute_WhenUsingAnyAndNoneFilters_ShouldMatchExpectedEntities()
	{
		var world = new World();

		var e1 = world.Spawn();
		world.Add(e1, new QueryPosition { X = 1, Y = 1 });
		world.Add(e1, new QueryVelocity { X = 2, Y = 0 });

		var e2 = world.Spawn();
		world.Add(e2, new QueryPosition { X = 1, Y = 1 });
		world.Add(e2, new QueryAcceleration { X = 1, Y = 1 });

		var e3 = world.Spawn();
		world.Add(e3, new QueryPosition { X = 1, Y = 1 });
		world.Add(e3, new QueryVelocity { X = 1, Y = 1 });
		world.Add(e3, new QueryFrozen());

		var handle = world.Compile<ActiveMotionQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();

		cursor.Current.Length.Should().Be(2);
	}

	[Fact]
	public void ForEach_WhenUsingTypedDelegate_ShouldApplyComponentUpdates()
	{
		var world = new World();

		var entity = world.Spawn();
		world.Add(entity, new QueryPosition { X = 1, Y = 2 });
		world.Add(entity, new QueryVelocity { X = 3, Y = 4 });
		var handle = world.Compile<PositionVelocityQuerySpec>();

		world.ForEach<PositionVelocityQuerySpec, QueryPosition, QueryVelocity>(
			handle,
			(ref QueryPosition position, in QueryVelocity velocity) =>
			{
				position.X += velocity.X;
				position.Y += velocity.Y;
			}
		);

		var updated = world.Get<QueryPosition>(entity);
		updated.X.Should().Be(4f);
		updated.Y.Should().Be(6f);
	}

	private readonly struct PositionVelocityQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<QueryPosition>();
			builder.All<QueryVelocity>();
		}
	}

	private readonly struct ActiveMotionQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<QueryPosition>();
			builder.Any<QueryVelocity>();
			builder.Any<QueryAcceleration>();
			builder.None<QueryFrozen>();
		}
	}
}

internal struct QueryPosition
{
	public float X;
	public float Y;
}

internal struct QueryVelocity
{
	public float X;
	public float Y;
}

internal struct QueryAcceleration
{
	public float X;
	public float Y;
}

internal struct QueryFrozen;
