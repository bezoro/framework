using Bezoro.ECS.Internal;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Internal;

[TestSubject(typeof(ComponentTypeRegistry))]
public class ComponentTypeRegistryTests
{
	[Fact]
	public void GetRelationshipIds_WhenCallerMutatesCopiedSnapshot_ShouldNotCorruptInternalState()
	{
		var registry = new ComponentTypeRegistry();
		int firstId  = registry.GetOrCreateRelationship(typeof(RelatesTo), new(1, 1));

		int[] snapshot = registry.GetRelationshipIds(typeof(RelatesTo)).ToArray();
		snapshot[0] = -123;

		var secondRead = registry.GetRelationshipIds(typeof(RelatesTo));
		secondRead.ToArray().Should().Equal(firstId);
	}

	[Fact]
	public void GetRelationshipIds_WhenNewRelationshipAdded_ShouldPreservePreviousSnapshotAndReturnNewOne()
	{
		var   registry                         = new ComponentTypeRegistry();
		int   firstId                          = registry.GetOrCreateRelationship(typeof(RelatesTo), new(1, 1));
		int[] snapshotBeforeSecondRelationship = registry.GetRelationshipIds(typeof(RelatesTo)).ToArray();

		int   secondId                        = registry.GetOrCreateRelationship(typeof(RelatesTo), new(2, 1));
		int[] snapshotAfterSecondRelationship = registry.GetRelationshipIds(typeof(RelatesTo)).ToArray();

		snapshotBeforeSecondRelationship.Should().Equal(firstId);
		snapshotAfterSecondRelationship.Should().Contain(firstId).And.Contain(secondId);
		snapshotAfterSecondRelationship.Length.Should().Be(2);
	}

	[Fact]
	public void GetRelationshipIds_WhenNoNewRelationshipsAdded_ShouldReturnEquivalentSnapshotValues()
	{
		var registry = new ComponentTypeRegistry();
		_ = registry.GetOrCreateRelationship(typeof(RelatesTo), new(1, 1));

		var firstSnapshot  = registry.GetRelationshipIds(typeof(RelatesTo));
		var secondSnapshot = registry.GetRelationshipIds(typeof(RelatesTo));

		firstSnapshot.ToArray().Should().Equal(secondSnapshot.ToArray());
	}

	[Fact]
	public void GetRelationshipIds_WhenRelationshipRemoved_ShouldPreservePreviousSnapshotAndReturnNewOne()
	{
		var   registry              = new ComponentTypeRegistry();
		var   firstTarget           = new Entity(1, 1);
		var   secondTarget          = new Entity(2, 1);
		int   firstId               = registry.GetOrCreateRelationship(typeof(RelatesTo), firstTarget);
		int   secondId              = registry.GetOrCreateRelationship(typeof(RelatesTo), secondTarget);
		int[] snapshotBeforeRemoval = registry.GetRelationshipIds(typeof(RelatesTo)).ToArray();

		registry.ReleaseRelationshipsForTarget(firstTarget);
		int[] snapshotAfterRemoval = registry.GetRelationshipIds(typeof(RelatesTo)).ToArray();

		snapshotBeforeRemoval.Should().Equal(firstId, secondId);
		snapshotAfterRemoval.Should().Equal(secondId);
	}

	private readonly struct RelatesTo;
}
