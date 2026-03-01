using Bezoro.ECS.Internal;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Internal;

[TestSubject(typeof(WorldRelationIndex))]
public class WorldRelationIndexTests
{
	[Fact]
	public void GetOrCreateRelationTypeId_WhenRelationAlreadyExists_ShouldReuseExistingTypeId()
	{
		var index   = new WorldRelationIndex();
		var target  = new Entity(7, 3);
		int created = 0;

		int first = index.GetOrCreateRelationTypeId(typeof(TestRelation), target, () => ++created);
		int second = index.GetOrCreateRelationTypeId(typeof(TestRelation), target, () => ++created);

		first.Should().Be(second);
		created.Should().Be(1);
	}

	[Fact]
	public void ReleaseRelationsForTarget_WhenTargetHasRegisteredRelations_ShouldRemoveEveryLookupAndNotifySources()
	{
		var index   = new WorldRelationIndex();
		var target  = new Entity(11, 2);
		int removed = 0;

		int first = index.GetOrCreateRelationTypeId(typeof(TestRelation), target, () => 100);
		int second = index.GetOrCreateRelationTypeId(typeof(OtherRelation), target, () => 101);

		index.ReleaseRelationsForTarget(target, _ => removed++);

		removed.Should().Be(2);
		index.TryGetRelationTypeId(typeof(TestRelation), target, out _).Should().BeFalse();
		index.TryGetRelationTypeId(typeof(OtherRelation), target, out _).Should().BeFalse();
		index.GetRelationTypeIds(typeof(TestRelation)).Should().BeEmpty();
		index.GetRelationTypeIds(typeof(OtherRelation)).Should().BeEmpty();
		index.TryGetRelationInfo(first, out _).Should().BeFalse();
		index.TryGetRelationInfo(second, out _).Should().BeFalse();
	}

	private struct OtherRelation;

	private struct TestRelation;
}
