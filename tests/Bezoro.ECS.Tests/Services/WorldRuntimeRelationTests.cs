using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

public partial class WorldRuntimeTests
{
	[Fact]
	public void Execute_WhenQueryUsesSpecificRelatedFilter_ShouldMatchOnlyEntitiesRelatedToTarget()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity        = 32,
				ComponentTypeCapacity = 64,
				QueryResultCapacity   = 32
			}
		);

		var targetA = world.Spawn();
		var targetB = world.Spawn();
		var first   = world.Spawn(new Position { X = 1, Y = 1 });
		var second  = world.Spawn(new Position { X = 2, Y = 2 });
		_ = world.Spawn(new Position { X = 3, Y = 3 });

		world.AddRelation<Follows>(first,  targetA);
		world.AddRelation<Follows>(second, targetB);

		var specificHandle = world.Compile<PositionRelatedToEntityZeroQuerySpec>();
		using (var specific = world.Execute(specificHandle))
		{
			specific.MoveNext().Should().BeTrue();
			specific.Current.ToArray().Should().ContainSingle().Which.Should().Be(first);
		}

		var       wildcardHandle = world.Compile<PositionRelatedToAnyFollowsQuerySpec>();
		using var wildcard       = world.Execute(wildcardHandle);
		wildcard.MoveNext().Should().BeTrue();
		var wildcardEntities = wildcard.Current.ToArray();
		wildcardEntities.Length.Should().Be(2);
		wildcardEntities.Should().Contain(first);
		wildcardEntities.Should().Contain(second);
	}


	[Fact]
	public void Execute_WhenRelationTargetIsDespawned_ShouldRemoveRelationAndNoLongerMatchRelatedQueries()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity        = 16,
				ComponentTypeCapacity = 64,
				QueryResultCapacity   = 16
			}
		);

		var target = world.Spawn();
		var source = world.Spawn(new Position { X = 1, Y = 1 });
		world.AddRelation<Follows>(source, target);

		world.HasRelation<Follows>(source, target).Should().BeTrue();
		world.Despawn(target);
		world.HasRelation<Follows>(source, target).Should().BeFalse();

		var       wildcardHandle = world.Compile<PositionRelatedToAnyFollowsQuerySpec>();
		using var wildcard       = world.Execute(wildcardHandle);
		wildcard.MoveNext().Should().BeTrue();
		wildcard.Current.Length.Should().Be(0);
	}

	[Fact]
	public void RelationApi_WhenAddingAndRemovingRelation_ShouldUpdateHasRelationState()
	{
		using var world  = new World();
		var       target = world.Spawn();
		var       source = world.Spawn();

		world.HasRelation<Follows>(source, target).Should().BeFalse();

		world.AddRelation<Follows>(source, target);
		world.HasRelation<Follows>(source, target).Should().BeTrue();
		world.HasRelation<Follows>(source, Entity.Wildcard).Should().BeTrue();

		world.RemoveRelation<Follows>(source, target).Should().BeTrue();
		world.HasRelation<Follows>(source, target).Should().BeFalse();
		world.RemoveRelation<Follows>(source, target).Should().BeFalse();
	}
}
