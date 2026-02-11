using System;
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
	public void GetRelationshipIds_WhenNoNewRelationshipsAdded_ShouldReturnEquivalentSnapshotValues()
	{
		var registry = new ComponentTypeRegistry();
		_ = registry.GetOrCreateRelationship(typeof(RelatesTo), new Entity(1, 1));

		ReadOnlySpan<int> firstSnapshot  = registry.GetRelationshipIds(typeof(RelatesTo));
		ReadOnlySpan<int> secondSnapshot = registry.GetRelationshipIds(typeof(RelatesTo));

		firstSnapshot.ToArray().Should().Equal(secondSnapshot.ToArray());
	}

	[Fact]
	public void GetRelationshipIds_WhenCallerMutatesCopiedSnapshot_ShouldNotCorruptInternalState()
	{
		var registry = new ComponentTypeRegistry();
		int firstId  = registry.GetOrCreateRelationship(typeof(RelatesTo), new Entity(1, 1));

		int[] snapshot = registry.GetRelationshipIds(typeof(RelatesTo)).ToArray();
		snapshot[0] = -123;

		ReadOnlySpan<int> secondRead = registry.GetRelationshipIds(typeof(RelatesTo));
		secondRead.ToArray().Should().Equal(firstId);
	}

	[Fact]
	public void GetRelationshipIds_WhenNewRelationshipAdded_ShouldPreservePreviousSnapshotAndReturnNewOne()
	{
		var registry = new ComponentTypeRegistry();
		int firstId  = registry.GetOrCreateRelationship(typeof(RelatesTo), new Entity(1, 1));
		int[] snapshotBeforeSecondRelationship = registry.GetRelationshipIds(typeof(RelatesTo)).ToArray();

		int secondId = registry.GetOrCreateRelationship(typeof(RelatesTo), new Entity(2, 1));
		int[] snapshotAfterSecondRelationship = registry.GetRelationshipIds(typeof(RelatesTo)).ToArray();

		snapshotBeforeSecondRelationship.Should().Equal(firstId);
		snapshotAfterSecondRelationship.Should().Contain(firstId).And.Contain(secondId);
		snapshotAfterSecondRelationship.Length.Should().Be(2);
	}

	private readonly struct RelatesTo;
}
