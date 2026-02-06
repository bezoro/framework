using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Services;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Internal;

[TestSubject(typeof(ComponentColumnFactory))]
public class ChunkColumnStorageTests
{
	[Fact]
	public void ColumnStorage_WhenComponentIsUnmanaged_ShouldUseUnmanagedColumn()
	{
		var world = new World();
		var entity = world.Spawn();
		world.Add(entity, new UnmanagedPosition { X = 1, Y = 2 });

		var archetype = world.GetOrCreateArchetype(typeof(UnmanagedPosition));
		var chunk = archetype.Chunks[0];
		int typeId = ComponentTypeRegistry.GetOrCreate<UnmanagedPosition>();
		int index = archetype.GetTypeIndex(typeId);

		chunk.IsUnmanagedColumn(index).Should().BeTrue();
	}

	[Fact]
	public void ColumnStorage_WhenComponentContainsReferences_ShouldUseManagedColumnAndPreserveData()
	{
		var world = new World();
		var entity = world.Spawn();
		world.Add(entity, new ManagedPayload { Name = "alpha" });

		var archetype = world.GetOrCreateArchetype(typeof(ManagedPayload));
		var chunk = archetype.Chunks[0];
		int typeId = ComponentTypeRegistry.GetOrCreate<ManagedPayload>();
		int index = archetype.GetTypeIndex(typeId);

		chunk.IsUnmanagedColumn(index).Should().BeFalse();
		world.Get<ManagedPayload>(entity).Name.Should().Be("alpha");
	}

	[Fact]
	public void UnmanagedColumn_WhenCreated_ShouldUseNativeAlignedAllocAndRespectAlignment()
	{
		using var column = (UnmanagedComponentColumn)ComponentColumnFactory.Create(typeof(UnmanagedPosition), 8);

		column.UsesNativeAlignedAlloc.Should().BeTrue();
		(column.AlignedAddress % (nuint)column.AlignmentBytes).Should().Be(0);
	}
}

internal struct UnmanagedPosition : IComponent
{
	public float X;
	public float Y;
}

internal struct ManagedPayload : IComponent
{
	public string? Name;
}
