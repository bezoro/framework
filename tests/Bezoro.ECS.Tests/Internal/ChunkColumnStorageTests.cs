using System;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Services;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Internal;

[TestSubject(typeof(ComponentColumn))]
public class ChunkColumnStorageTests
{
	[Fact]
	public void ColumnStorage_WhenComponentContainsReferences_ShouldUseManagedColumnAndPreserveData()
	{
		var world  = new WorldV1();
		var entity = world.Spawn();
		world.Add(entity, new ManagedPayload { Name = "alpha" });

		var archetype = world.GetOrCreateArchetype(typeof(ManagedPayload));
		var chunk     = archetype.Chunks[0];
		int typeId    = world.GetOrCreateComponentTypeId<ManagedPayload>();
		int index     = archetype.GetTypeIndex(typeId);

		chunk.IsUnmanagedColumn(index).Should().BeFalse();
		world.Get<ManagedPayload>(entity).Name.Should().Be("alpha");
	}

	[Fact]
	public void ColumnStorage_WhenComponentIsUnmanaged_ShouldUseUnmanagedColumn()
	{
		var world  = new WorldV1();
		var entity = world.Spawn();
		world.Add(entity, new UnmanagedPosition { X = 1, Y = 2 });

		var archetype = world.GetOrCreateArchetype(typeof(UnmanagedPosition));
		var chunk     = archetype.Chunks[0];
		int typeId    = world.GetOrCreateComponentTypeId<UnmanagedPosition>();
		int index     = archetype.GetTypeIndex(typeId);

		chunk.IsUnmanagedColumn(index).Should().BeTrue();
	}

	[Fact]
	public void ColumnStorage_WhenComponentContainsBoolField_ShouldUseManagedColumn()
	{
		var world  = new WorldV1();
		var entity = world.Spawn();
		world.Add(entity, new BoolPayload { IsEnabled = true });

		var archetype = world.GetOrCreateArchetype(typeof(BoolPayload));
		var chunk     = archetype.Chunks[0];
		int typeId    = world.GetOrCreateComponentTypeId<BoolPayload>();
		int index     = archetype.GetTypeIndex(typeId);

		chunk.IsUnmanagedColumn(index).Should().BeFalse();
		world.Get<BoolPayload>(entity).IsEnabled.Should().BeTrue();
	}

	[Fact]
	public void UnmanagedColumn_WhenCreated_ShouldUseNativeAlignedAllocAndRespectAlignment()
	{
		using var column = ComponentColumn.Create(typeof(UnmanagedPosition), 8);

		column.UsesNativeAlignedAlloc.Should().BeTrue();
		(column.AlignedAddress % (nuint)column.AlignmentBytes).Should().Be(0);
	}

	[Fact]
	public void UnmanagedColumn_WhenDisposed_ShouldThrowWhenAccessingReference()
	{
		var column = ComponentColumn.Create(typeof(UnmanagedPosition), 8);
		column.Dispose();

		var act = () =>
		{
			_ = column.GetReference<UnmanagedPosition>(0);
		};

		act.Should().Throw<ObjectDisposedException>();
	}
}

internal struct ManagedPayload{
	public string? Name;
}

internal struct UnmanagedPosition{
	public float X;
	public float Y;
}

internal struct BoolPayload{
	public bool IsEnabled;
}
