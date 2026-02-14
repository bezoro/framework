using System;
using Bezoro.ECS.Internal.Fixed;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Internal.Fixed;

[TestSubject(typeof(ArchetypeStorage))]
public class ArchetypeStorageTests
{
	[Fact]
	public void ColumnStorage_WhenTypeIsUnmanaged_ShouldUseNativeBackedColumn()
	{
		using var storage = CreateStorage(typeof(UnmanagedComponent), isManagedLane: false);
		storage.AllocateRow(1, out int chunkIndex, out int rowIndex);

		ref var component = ref storage.GetRef<UnmanagedComponent>(chunkIndex, rowIndex, 0);
		component.Value = 42;

		var chunk = storage.GetChunk(chunkIndex);
		chunk.Columns[0].IsUnmanaged.Should().BeTrue();
		storage.GetRef<UnmanagedComponent>(chunkIndex, rowIndex, 0).Value.Should().Be(42);
	}

	[Fact]
	public void ColumnStorage_WhenTypeIsManagedLane_ShouldUseManagedColumn()
	{
		using var storage = CreateStorage(typeof(ManagedComponent), isManagedLane: true);
		storage.AllocateRow(1, out int chunkIndex, out int rowIndex);
		ref var component = ref storage.GetRef<ManagedComponent>(chunkIndex, rowIndex, 0);
		component.Name = "alpha";

		var chunk = storage.GetChunk(chunkIndex);
		chunk.Columns[0].IsUnmanaged.Should().BeFalse();
		storage.TryGetValue(chunkIndex, rowIndex, 0, out ManagedComponent restored).Should().BeTrue();
		restored.Name.Should().Be("alpha");
	}

	[Fact]
	public void MoveRowRangeWithinChunk_WhenCompactingRows_ShouldMoveEntitiesAndComponentDataTogether()
	{
		using var storage = CreateStorage(typeof(UnmanagedComponent), isManagedLane: false, chunkCapacity: 8);
		for (var i = 0; i < 4; i++)
		{
			storage.AllocateRow(i + 1, out int chunkIndex, out int rowIndex);
			ref var component = ref storage.GetRef<UnmanagedComponent>(chunkIndex, rowIndex, 0);
			component.Value = i + 100;
		}

		var chunk = storage.GetChunk(0);
		storage.MoveRowRangeWithinChunk(chunk, sourceRowIndex: 2, destinationRowIndex: 0, rowCount: 2);

		chunk.EntityIds[0].Should().Be(3);
		chunk.EntityIds[1].Should().Be(4);
		storage.GetRef<UnmanagedComponent>(0, 0, 0).Value.Should().Be(102);
		storage.GetRef<UnmanagedComponent>(0, 1, 0).Value.Should().Be(103);
	}

	[Fact]
	public void MoveRowRangeWithinChunk_WhenSourceAndDestinationOverlap_ShouldPreserveRangeOrdering()
	{
		using var storage = CreateStorage(typeof(UnmanagedComponent), isManagedLane: false, chunkCapacity: 8);
		for (var i = 0; i < 5; i++)
		{
			storage.AllocateRow(i + 1, out int chunkIndex, out int rowIndex);
			ref var component = ref storage.GetRef<UnmanagedComponent>(chunkIndex, rowIndex, 0);
			component.Value = i + 10;
		}

		var chunk = storage.GetChunk(0);
		storage.MoveRowRangeWithinChunk(chunk, sourceRowIndex: 0, destinationRowIndex: 1, rowCount: 4);

		chunk.EntityIds[1].Should().Be(1);
		chunk.EntityIds[2].Should().Be(2);
		chunk.EntityIds[3].Should().Be(3);
		chunk.EntityIds[4].Should().Be(4);
		storage.GetRef<UnmanagedComponent>(0, 1, 0).Value.Should().Be(10);
		storage.GetRef<UnmanagedComponent>(0, 2, 0).Value.Should().Be(11);
		storage.GetRef<UnmanagedComponent>(0, 3, 0).Value.Should().Be(12);
		storage.GetRef<UnmanagedComponent>(0, 4, 0).Value.Should().Be(13);
	}

	[Fact]
	public void CopySharedColumnsFromWithPairs_WhenCopyingRange_ShouldCopyAllRequestedRows()
	{
		using var source = CreateStorage(typeof(UnmanagedComponent), isManagedLane: false, chunkCapacity: 8);
		using var target = CreateStorage(typeof(UnmanagedComponent), isManagedLane: false, chunkCapacity: 8);
		for (var i = 0; i < 5; i++)
		{
			source.AllocateRow(i + 1, out int sourceChunkIndex, out int sourceRowIndex);
			ref var component = ref source.GetRef<UnmanagedComponent>(sourceChunkIndex, sourceRowIndex, 0);
			component.Value = i + 10;
		}

		target.ReserveRows(3, out int targetChunkIndex, out int targetRowStart);
		source.CopySharedColumnsFromWithPairs(
			source.GetChunk(0),
			sourceRowIndex: 1,
			target.GetChunk(targetChunkIndex),
			targetRowIndex: targetRowStart,
			rowCount: 3,
			sourceTargetColumnPairs: [0, 0]
		);

		target.GetRef<UnmanagedComponent>(targetChunkIndex, targetRowStart, 0).Value.Should().Be(11);
		target.GetRef<UnmanagedComponent>(targetChunkIndex, targetRowStart + 1, 0).Value.Should().Be(12);
		target.GetRef<UnmanagedComponent>(targetChunkIndex, targetRowStart + 2, 0).Value.Should().Be(13);
	}

	private static ArchetypeStorage CreateStorage(Type componentType, bool isManagedLane, int chunkCapacity = 4) =>
		new(
			id: 0,
			typeIds: [0],
			maskWords: [1UL],
			columnTypes: [componentType],
			columnIsManagedLane: [isManagedLane],
			componentTypeCapacity: 8,
			chunkCapacity: chunkCapacity
		);

	private struct UnmanagedComponent
	{
		public int Value;
	}

	private struct ManagedComponent
	{
		public string? Name;
	}
}
