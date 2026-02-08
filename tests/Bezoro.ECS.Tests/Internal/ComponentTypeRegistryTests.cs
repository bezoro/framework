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
	public void GetRelationshipIds_WhenNoNewRelationshipsAdded_ShouldReturnSameSnapshotReference()
	{
		var registry = new ComponentTypeRegistry();
		_ = registry.GetOrCreateRelationship(typeof(RelatesTo), new Entity(1, 1));

		int[] firstSnapshot  = registry.GetRelationshipIds(typeof(RelatesTo));
		int[] secondSnapshot = registry.GetRelationshipIds(typeof(RelatesTo));

		ReferenceEquals(firstSnapshot, secondSnapshot).Should().BeTrue();
	}

	[Fact]
	public void GetRelationshipIds_WhenNewRelationshipAdded_ShouldPreservePreviousSnapshotAndReturnNewOne()
	{
		var registry = new ComponentTypeRegistry();
		int firstId  = registry.GetOrCreateRelationship(typeof(RelatesTo), new Entity(1, 1));
		int[] snapshotBeforeSecondRelationship = registry.GetRelationshipIds(typeof(RelatesTo));

		int secondId = registry.GetOrCreateRelationship(typeof(RelatesTo), new Entity(2, 1));
		int[] snapshotAfterSecondRelationship = registry.GetRelationshipIds(typeof(RelatesTo));

		snapshotBeforeSecondRelationship.Should().Equal(firstId);
		snapshotAfterSecondRelationship.Should().Contain(firstId).And.Contain(secondId);
		snapshotAfterSecondRelationship.Length.Should().Be(2);
	}

	private readonly struct RelatesTo;
}
