using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(World))]
public class SplitFieldGeneratorTests
{
	[Fact]
	public void SplitGeneratedGroups_WhenQueriedByHotGroup_ShouldIterateWithoutColdGroup()
	{
		var world  = new World();
		var first  = world.Spawn();
		var second = world.Spawn();

		SplitTransformSplitGenerated.Add(
			world, first, new() { PositionX = 1f, PositionY = 2f, RotationZ = 3f, Scale = 4f }
		);

		SplitTransformSplitGenerated.Add(
			world, second, new() { PositionX = 5f, PositionY = 6f, RotationZ = 7f, Scale = 8f }
		);

		var sum = 0f;
		world.Query().All<SplitTransformSplitGenerated.Group0>().ForEach((ref SplitTransformSplitGenerated.Group0 group) =>
			{
				sum += group.PositionX +
					   group.PositionY +
					   group.RotationZ;
			}
		);

		sum.Should().Be(24f);
	}

	[Fact]
	public void SplitGeneratedHelpers_WhenAddingSplitComponent_ShouldStoreAndRehydrateGroups()
	{
		var world  = new World();
		var entity = world.Spawn();
		var input = new SplitTransform
		{
			PositionX = 3f,
			PositionY = 4f,
			RotationZ = 90f,
			Scale     = 2f
		};

		SplitTransformSplitGenerated.Add(world, entity, in input);

		world.Has<SplitTransformSplitGenerated.Group0>(entity).Should().BeTrue();
		world.Has<SplitTransformSplitGenerated.Group1>(entity).Should().BeTrue();
		SplitTransformSplitGenerated.TryGet(world, entity, out var restored).Should().BeTrue();
		restored.Should().BeEquivalentTo(input);
	}
}

[SplitFields]
internal struct SplitTransform{
	[SplitGroup(0)] public float PositionX;
	[SplitGroup(0)] public float PositionY;
	[SplitGroup(0)] public float RotationZ;
	[SplitGroup(1)] public float Scale;
}
