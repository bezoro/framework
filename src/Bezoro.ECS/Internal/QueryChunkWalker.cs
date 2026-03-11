using System.Runtime.CompilerServices;
using Bezoro.ECS.Internal.Fixed;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal interface IChunkAction<T1> where T1 : struct
{
	void Invoke(ref T1 component1);
}

internal interface IChunkAction<T1, T2>
	where T1 : struct
	where T2 : struct
{
	void Invoke(ref T1 component1, in T2 component2);
}

internal interface IChunkAction<T1, T2, T3>
	where T1 : struct
	where T2 : struct
	where T3 : struct
{
	void Invoke(ref T1 component1, in T2 component2, in T3 component3);
}

internal interface IChunkAction<T1, T2, T3, T4>
	where T1 : struct
	where T2 : struct
	where T3 : struct
	where T4 : struct
{
	void Invoke(ref T1 component1, in T2 component2, in T3 component3, in T4 component4);
}

internal interface IEntityChunkAction<T1> where T1 : struct
{
	void Invoke(Entity entity, ref T1 component1);
}

internal interface IEntityChunkAction<T1, T2>
	where T1 : struct
	where T2 : struct
{
	void Invoke(Entity entity, ref T1 component1, in T2 component2);
}

internal interface IEntityChunkAction<T1, T2, T3>
	where T1 : struct
	where T2 : struct
	where T3 : struct
{
	void Invoke(Entity entity, ref T1 component1, in T2 component2, in T3 component3);
}

internal interface IEntityChunkAction<T1, T2, T3, T4>
	where T1 : struct
	where T2 : struct
	where T3 : struct
	where T4 : struct
{
	void Invoke(Entity entity, ref T1 component1, in T2 component2, in T3 component3, in T4 component4);
}

// TODO: [CODE SMELL - Duplicated code] This walker repeats near-identical arity-specific traversal loops for chunk and entity execution. Fix: collapse these overloads onto a smaller shared kernel only after confirming the extracted abstraction does not regress hot-path performance.
internal static class QueryChunkWalker
{
	public static void Execute<TAction, T1>(
		World             world,
		QueryChunkMatch[] chunkMatches,
		int               chunkMatchCount,
		TAction           action)
		where TAction : struct, IChunkAction<T1>
		where T1 : struct
	{
		int               typeId1            = world.GetOrCreateComponentTypeId<T1>();
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = chunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = world.GetArchetypeForCursor(match.ArchetypeId);
				cachedColumnIndex1 = GetColumnIndex(cachedArchetype, typeId1, match.ArchetypeId);
			}

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			if (match.Count == 0)
				continue;

			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			for (var offset = 0; offset < match.Count; offset++)
				action.Invoke(ref Unsafe.Add(ref c1Start, offset));
		}
	}

	public static void Execute<TAction, T1, T2>(
		World             world,
		QueryChunkMatch[] chunkMatches,
		int               chunkMatchCount,
		TAction           action)
		where TAction : struct, IChunkAction<T1, T2>
		where T1 : struct
		where T2 : struct
	{
		int               typeId1            = world.GetOrCreateComponentTypeId<T1>();
		int               typeId2            = world.GetOrCreateComponentTypeId<T2>();
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		int               cachedColumnIndex2 = -1;
		world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = chunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = world.GetArchetypeForCursor(match.ArchetypeId);
				cachedColumnIndex1 = GetColumnIndex(cachedArchetype, typeId1, match.ArchetypeId);
				cachedColumnIndex2 = GetColumnIndex(cachedArchetype, typeId2, match.ArchetypeId);
			}

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			if (match.Count == 0)
				continue;

			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			ref var c2Start = ref cachedArchetype.GetRefByIndex<T2>(chunk, cachedColumnIndex2, match.RowStart);
			for (var offset = 0; offset < match.Count; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				action.Invoke(ref c1, in c2);
			}
		}
	}

	public static void Execute<TAction, T1, T2, T3>(
		World             world,
		QueryChunkMatch[] chunkMatches,
		int               chunkMatchCount,
		TAction           action)
		where TAction : struct, IChunkAction<T1, T2, T3>
		where T1 : struct
		where T2 : struct
		where T3 : struct
	{
		int               typeId1            = world.GetOrCreateComponentTypeId<T1>();
		int               typeId2            = world.GetOrCreateComponentTypeId<T2>();
		int               typeId3            = world.GetOrCreateComponentTypeId<T3>();
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		int               cachedColumnIndex2 = -1;
		int               cachedColumnIndex3 = -1;
		world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = chunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = world.GetArchetypeForCursor(match.ArchetypeId);
				cachedColumnIndex1 = GetColumnIndex(cachedArchetype, typeId1, match.ArchetypeId);
				cachedColumnIndex2 = GetColumnIndex(cachedArchetype, typeId2, match.ArchetypeId);
				cachedColumnIndex3 = GetColumnIndex(cachedArchetype, typeId3, match.ArchetypeId);
			}

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			if (match.Count == 0)
				continue;

			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			ref var c2Start = ref cachedArchetype.GetRefByIndex<T2>(chunk, cachedColumnIndex2, match.RowStart);
			ref var c3Start = ref cachedArchetype.GetRefByIndex<T3>(chunk, cachedColumnIndex3, match.RowStart);
			for (var offset = 0; offset < match.Count; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				ref var c3 = ref Unsafe.Add(ref c3Start, offset);
				action.Invoke(ref c1, in c2, in c3);
			}
		}
	}

	public static void Execute<TAction, T1, T2, T3, T4>(
		World             world,
		QueryChunkMatch[] chunkMatches,
		int               chunkMatchCount,
		TAction           action)
		where TAction : struct, IChunkAction<T1, T2, T3, T4>
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct
	{
		int               typeId1            = world.GetOrCreateComponentTypeId<T1>();
		int               typeId2            = world.GetOrCreateComponentTypeId<T2>();
		int               typeId3            = world.GetOrCreateComponentTypeId<T3>();
		int               typeId4            = world.GetOrCreateComponentTypeId<T4>();
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		int               cachedColumnIndex2 = -1;
		int               cachedColumnIndex3 = -1;
		int               cachedColumnIndex4 = -1;
		world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = chunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = world.GetArchetypeForCursor(match.ArchetypeId);
				cachedColumnIndex1 = GetColumnIndex(cachedArchetype, typeId1, match.ArchetypeId);
				cachedColumnIndex2 = GetColumnIndex(cachedArchetype, typeId2, match.ArchetypeId);
				cachedColumnIndex3 = GetColumnIndex(cachedArchetype, typeId3, match.ArchetypeId);
				cachedColumnIndex4 = GetColumnIndex(cachedArchetype, typeId4, match.ArchetypeId);
			}

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			if (match.Count == 0)
				continue;

			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			ref var c2Start = ref cachedArchetype.GetRefByIndex<T2>(chunk, cachedColumnIndex2, match.RowStart);
			ref var c3Start = ref cachedArchetype.GetRefByIndex<T3>(chunk, cachedColumnIndex3, match.RowStart);
			ref var c4Start = ref cachedArchetype.GetRefByIndex<T4>(chunk, cachedColumnIndex4, match.RowStart);
			for (var offset = 0; offset < match.Count; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				ref var c3 = ref Unsafe.Add(ref c3Start, offset);
				ref var c4 = ref Unsafe.Add(ref c4Start, offset);
				action.Invoke(ref c1, in c2, in c3, in c4);
			}
		}
	}

	public static void ExecuteEntity<TAction, T1>(
		World             world,
		QueryChunkMatch[] chunkMatches,
		int               chunkMatchCount,
		TAction           action)
		where TAction : struct, IEntityChunkAction<T1>
		where T1 : struct
	{
		int               typeId1            = world.GetOrCreateComponentTypeId<T1>();
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		var versions = world.GetEntityVersionsForCursor();
		world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = chunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = world.GetArchetypeForCursor(match.ArchetypeId);
				cachedColumnIndex1 = GetColumnIndex(cachedArchetype, typeId1, match.ArchetypeId);
			}

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			if (match.Count == 0)
				continue;

			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			ref var entityIdStart = ref chunk.EntityIds[match.RowStart];
			for (var offset = 0; offset < match.Count; offset++)
			{
				int entityId = Unsafe.Add(ref entityIdStart, offset);
				action.Invoke(new(entityId, versions[entityId]), ref Unsafe.Add(ref c1Start, offset));
			}
		}
	}

	public static void ExecuteEntity<TAction, T1, T2>(
		World             world,
		QueryChunkMatch[] chunkMatches,
		int               chunkMatchCount,
		TAction           action)
		where TAction : struct, IEntityChunkAction<T1, T2>
		where T1 : struct
		where T2 : struct
	{
		int               typeId1            = world.GetOrCreateComponentTypeId<T1>();
		int               typeId2            = world.GetOrCreateComponentTypeId<T2>();
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		int               cachedColumnIndex2 = -1;
		var versions = world.GetEntityVersionsForCursor();
		world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = chunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = world.GetArchetypeForCursor(match.ArchetypeId);
				cachedColumnIndex1 = GetColumnIndex(cachedArchetype, typeId1, match.ArchetypeId);
				cachedColumnIndex2 = GetColumnIndex(cachedArchetype, typeId2, match.ArchetypeId);
			}

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			if (match.Count == 0)
				continue;

			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			ref var c2Start = ref cachedArchetype.GetRefByIndex<T2>(chunk, cachedColumnIndex2, match.RowStart);
			ref var entityIdStart = ref chunk.EntityIds[match.RowStart];
			for (var offset = 0; offset < match.Count; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				int entityId = Unsafe.Add(ref entityIdStart, offset);
				action.Invoke(new(entityId, versions[entityId]), ref c1, in c2);
			}
		}
	}

	public static void ExecuteEntity<TAction, T1, T2, T3>(
		World             world,
		QueryChunkMatch[] chunkMatches,
		int               chunkMatchCount,
		TAction           action)
		where TAction : struct, IEntityChunkAction<T1, T2, T3>
		where T1 : struct
		where T2 : struct
		where T3 : struct
	{
		int               typeId1            = world.GetOrCreateComponentTypeId<T1>();
		int               typeId2            = world.GetOrCreateComponentTypeId<T2>();
		int               typeId3            = world.GetOrCreateComponentTypeId<T3>();
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		int               cachedColumnIndex2 = -1;
		int               cachedColumnIndex3 = -1;
		var versions = world.GetEntityVersionsForCursor();
		world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = chunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = world.GetArchetypeForCursor(match.ArchetypeId);
				cachedColumnIndex1 = GetColumnIndex(cachedArchetype, typeId1, match.ArchetypeId);
				cachedColumnIndex2 = GetColumnIndex(cachedArchetype, typeId2, match.ArchetypeId);
				cachedColumnIndex3 = GetColumnIndex(cachedArchetype, typeId3, match.ArchetypeId);
			}

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			if (match.Count == 0)
				continue;

			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			ref var c2Start = ref cachedArchetype.GetRefByIndex<T2>(chunk, cachedColumnIndex2, match.RowStart);
			ref var c3Start = ref cachedArchetype.GetRefByIndex<T3>(chunk, cachedColumnIndex3, match.RowStart);
			ref var entityIdStart = ref chunk.EntityIds[match.RowStart];
			for (var offset = 0; offset < match.Count; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				ref var c3 = ref Unsafe.Add(ref c3Start, offset);
				int entityId = Unsafe.Add(ref entityIdStart, offset);
				action.Invoke(new(entityId, versions[entityId]), ref c1, in c2, in c3);
			}
		}
	}

	public static void ExecuteEntity<TAction, T1, T2, T3, T4>(
		World             world,
		QueryChunkMatch[] chunkMatches,
		int               chunkMatchCount,
		TAction           action)
		where TAction : struct, IEntityChunkAction<T1, T2, T3, T4>
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct
	{
		int               typeId1            = world.GetOrCreateComponentTypeId<T1>();
		int               typeId2            = world.GetOrCreateComponentTypeId<T2>();
		int               typeId3            = world.GetOrCreateComponentTypeId<T3>();
		int               typeId4            = world.GetOrCreateComponentTypeId<T4>();
		int               cachedArchetypeId  = int.MinValue;
		ArchetypeStorage? cachedArchetype    = null;
		int               cachedColumnIndex1 = -1;
		int               cachedColumnIndex2 = -1;
		int               cachedColumnIndex3 = -1;
		int               cachedColumnIndex4 = -1;
		var versions = world.GetEntityVersionsForCursor();
		world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
		for (var i = 0; i < chunkMatchCount; i++)
		{
			var match = chunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId  = match.ArchetypeId;
				cachedArchetype    = world.GetArchetypeForCursor(match.ArchetypeId);
				cachedColumnIndex1 = GetColumnIndex(cachedArchetype, typeId1, match.ArchetypeId);
				cachedColumnIndex2 = GetColumnIndex(cachedArchetype, typeId2, match.ArchetypeId);
				cachedColumnIndex3 = GetColumnIndex(cachedArchetype, typeId3, match.ArchetypeId);
				cachedColumnIndex4 = GetColumnIndex(cachedArchetype, typeId4, match.ArchetypeId);
			}

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			if (match.Count == 0)
				continue;

			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			ref var c2Start = ref cachedArchetype.GetRefByIndex<T2>(chunk, cachedColumnIndex2, match.RowStart);
			ref var c3Start = ref cachedArchetype.GetRefByIndex<T3>(chunk, cachedColumnIndex3, match.RowStart);
			ref var c4Start = ref cachedArchetype.GetRefByIndex<T4>(chunk, cachedColumnIndex4, match.RowStart);
			ref var entityIdStart = ref chunk.EntityIds[match.RowStart];
			for (var offset = 0; offset < match.Count; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				ref var c3 = ref Unsafe.Add(ref c3Start, offset);
				ref var c4 = ref Unsafe.Add(ref c4Start, offset);
				int entityId = Unsafe.Add(ref entityIdStart, offset);
				action.Invoke(new(entityId, versions[entityId]), ref c1, in c2, in c3, in c4);
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int GetColumnIndex(ArchetypeStorage archetype, int typeId, int archetypeId)
	{
		int columnIndex = archetype.GetColumnIndexOrNegative(typeId);
		if (columnIndex >= 0)
			return columnIndex;

		throw new KeyNotFoundException($"Type id '{typeId}' does not exist in archetype '{archetypeId}'.");
	}
}
