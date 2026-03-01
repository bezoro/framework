using System.Runtime.CompilerServices;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal.Fixed;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class WorldDirectIterationService(World world)
{
	private readonly World _world = world;

	public void RunParallel<TSpec, TJob, T1>(QueryHandle<TSpec> handle, TJob job, int? degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1>
		where T1 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		int parallelism = _world.ResolveDegreeOfParallelismForDirectIteration(degreeOfParallelism);
		_world.AcquireQueryChunkMatchScratchForDirectIteration(out var chunkMatches, out bool usesSharedScratch);
		try
		{
			int entityCount = _world.FillQueryResultsForQueryEngine(
				handle.Plan,
				chunkMatches,
				_world.QueryResultCapacityForDirectIteration,
				out int chunkMatchCount
			);
			if (entityCount == 0)
				return;

			int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
			_world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
			ParallelWorkScheduler.Execute(
				chunkMatchCount,
				parallelism,
				index =>
				{
					var match = chunkMatches[index];
					if (match.Count == 0)
						return;

					var archetype = _world.GetArchetypeForCursor(match.ArchetypeId);
					int columnIndex1 = GetColumnIndex(archetype, typeId1, match.ArchetypeId);
					var chunk = archetype.GetChunkUnchecked(match.ChunkIndex);
					ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, match.RowStart);
					var local = job;
					for (var offset = 0; offset < match.Count; offset++)
						local.Execute(ref Unsafe.Add(ref c1Start, offset));
				}
			);
		}
		finally
		{
			_world.ReleaseQueryChunkMatchScratchForDirectIteration(chunkMatches, usesSharedScratch);
		}
	}

	public void RunParallel<TSpec, TJob, T1, T2>(QueryHandle<TSpec> handle, TJob job, int? degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		int parallelism = _world.ResolveDegreeOfParallelismForDirectIteration(degreeOfParallelism);
		_world.AcquireQueryChunkMatchScratchForDirectIteration(out var chunkMatches, out bool usesSharedScratch);
		try
		{
			int entityCount = _world.FillQueryResultsForQueryEngine(
				handle.Plan,
				chunkMatches,
				_world.QueryResultCapacityForDirectIteration,
				out int chunkMatchCount
			);
			if (entityCount == 0)
				return;

			int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
			int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
			_world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
			ParallelWorkScheduler.Execute(
				chunkMatchCount,
				parallelism,
				index =>
				{
					var match = chunkMatches[index];
					if (match.Count == 0)
						return;

					var archetype = _world.GetArchetypeForCursor(match.ArchetypeId);
					int columnIndex1 = GetColumnIndex(archetype, typeId1, match.ArchetypeId);
					int columnIndex2 = GetColumnIndex(archetype, typeId2, match.ArchetypeId);
					var chunk = archetype.GetChunkUnchecked(match.ChunkIndex);
					ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, match.RowStart);
					ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, columnIndex2, match.RowStart);
					var local = job;
					for (var offset = 0; offset < match.Count; offset++)
					{
						ref var c1 = ref Unsafe.Add(ref c1Start, offset);
						ref var c2 = ref Unsafe.Add(ref c2Start, offset);
						local.Execute(ref c1, in c2);
					}
				}
			);
		}
		finally
		{
			_world.ReleaseQueryChunkMatchScratchForDirectIteration(chunkMatches, usesSharedScratch);
		}
	}

	public void RunParallel<TSpec, TJob, T1, T2, T3>(
		QueryHandle<TSpec> handle,
		TJob               job,
		int?               degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		int parallelism = _world.ResolveDegreeOfParallelismForDirectIteration(degreeOfParallelism);
		_world.AcquireQueryChunkMatchScratchForDirectIteration(out var chunkMatches, out bool usesSharedScratch);
		try
		{
			int entityCount = _world.FillQueryResultsForQueryEngine(
				handle.Plan,
				chunkMatches,
				_world.QueryResultCapacityForDirectIteration,
				out int chunkMatchCount
			);
			if (entityCount == 0)
				return;

			int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
			int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
			int typeId3 = _world.GetOrCreateComponentTypeId<T3>();
			_world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
			ParallelWorkScheduler.Execute(
				chunkMatchCount,
				parallelism,
				index =>
				{
					var match = chunkMatches[index];
					if (match.Count == 0)
						return;

					var archetype = _world.GetArchetypeForCursor(match.ArchetypeId);
					int columnIndex1 = GetColumnIndex(archetype, typeId1, match.ArchetypeId);
					int columnIndex2 = GetColumnIndex(archetype, typeId2, match.ArchetypeId);
					int columnIndex3 = GetColumnIndex(archetype, typeId3, match.ArchetypeId);
					var chunk = archetype.GetChunkUnchecked(match.ChunkIndex);
					ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, match.RowStart);
					ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, columnIndex2, match.RowStart);
					ref var c3Start = ref archetype.GetRefByIndex<T3>(chunk, columnIndex3, match.RowStart);
					var local = job;
					for (var offset = 0; offset < match.Count; offset++)
					{
						ref var c1 = ref Unsafe.Add(ref c1Start, offset);
						ref var c2 = ref Unsafe.Add(ref c2Start, offset);
						ref var c3 = ref Unsafe.Add(ref c3Start, offset);
						local.Execute(ref c1, in c2, in c3);
					}
				}
			);
		}
		finally
		{
			_world.ReleaseQueryChunkMatchScratchForDirectIteration(chunkMatches, usesSharedScratch);
		}
	}

	public void RunParallel<TSpec, TJob, T1, T2, T3, T4>(
		QueryHandle<TSpec> handle,
		TJob               job,
		int?               degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		int parallelism = _world.ResolveDegreeOfParallelismForDirectIteration(degreeOfParallelism);
		_world.AcquireQueryChunkMatchScratchForDirectIteration(out var chunkMatches, out bool usesSharedScratch);
		try
		{
			int entityCount = _world.FillQueryResultsForQueryEngine(
				handle.Plan,
				chunkMatches,
				_world.QueryResultCapacityForDirectIteration,
				out int chunkMatchCount
			);
			if (entityCount == 0)
				return;

			int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
			int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
			int typeId3 = _world.GetOrCreateComponentTypeId<T3>();
			int typeId4 = _world.GetOrCreateComponentTypeId<T4>();
			_world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
			ParallelWorkScheduler.Execute(
				chunkMatchCount,
				parallelism,
				index =>
				{
					var match = chunkMatches[index];
					if (match.Count == 0)
						return;

					var archetype = _world.GetArchetypeForCursor(match.ArchetypeId);
					int columnIndex1 = GetColumnIndex(archetype, typeId1, match.ArchetypeId);
					int columnIndex2 = GetColumnIndex(archetype, typeId2, match.ArchetypeId);
					int columnIndex3 = GetColumnIndex(archetype, typeId3, match.ArchetypeId);
					int columnIndex4 = GetColumnIndex(archetype, typeId4, match.ArchetypeId);
					var chunk = archetype.GetChunkUnchecked(match.ChunkIndex);
					ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, match.RowStart);
					ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, columnIndex2, match.RowStart);
					ref var c3Start = ref archetype.GetRefByIndex<T3>(chunk, columnIndex3, match.RowStart);
					ref var c4Start = ref archetype.GetRefByIndex<T4>(chunk, columnIndex4, match.RowStart);
					var local = job;
					for (var offset = 0; offset < match.Count; offset++)
					{
						ref var c1 = ref Unsafe.Add(ref c1Start, offset);
						ref var c2 = ref Unsafe.Add(ref c2Start, offset);
						ref var c3 = ref Unsafe.Add(ref c3Start, offset);
						ref var c4 = ref Unsafe.Add(ref c4Start, offset);
						local.Execute(ref c1, in c2, in c3, in c4);
					}
				}
			);
		}
		finally
		{
			_world.ReleaseQueryChunkMatchScratchForDirectIteration(chunkMatches, usesSharedScratch);
		}
	}

	public void RunParallelEntity<TSpec, TJob, T1>(QueryHandle<TSpec> handle, TJob job, int? degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1>
		where T1 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		int parallelism = _world.ResolveDegreeOfParallelismForDirectIteration(degreeOfParallelism);
		_world.AcquireQueryExecutionScratchForDirectIteration(out var chunkMatches, out var entities, out bool usesSharedScratch);
		try
		{
			int entityCount = _world.FillQueryResultsForQueryEngine(
				handle.Plan,
				chunkMatches,
				_world.QueryResultCapacityForDirectIteration,
				out int chunkMatchCount
			);
			if (entityCount == 0)
				return;

			_world.MaterializeQueryEntities(chunkMatches, chunkMatchCount, entities, entityCount);
			int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
			_world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
			ParallelWorkScheduler.Execute(
				chunkMatchCount,
				parallelism,
				index =>
				{
					var match = chunkMatches[index];
					if (match.Count == 0)
						return;

					var archetype = _world.GetArchetypeForCursor(match.ArchetypeId);
					int columnIndex1 = GetColumnIndex(archetype, typeId1, match.ArchetypeId);
					var chunk = archetype.GetChunkUnchecked(match.ChunkIndex);
					ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, match.RowStart);
					var local = job;
					int entityIndex = match.EntityStartIndex;
					for (var offset = 0; offset < match.Count; offset++)
						local.Execute(entities[entityIndex + offset], ref Unsafe.Add(ref c1Start, offset));
				}
			);
		}
		finally
		{
			_world.ReleaseQueryExecutionScratchForDirectIteration(chunkMatches, entities, usesSharedScratch);
		}
	}

	public void RunParallelEntity<TSpec, TJob, T1, T2>(
		QueryHandle<TSpec> handle,
		TJob               job,
		int?               degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		int parallelism = _world.ResolveDegreeOfParallelismForDirectIteration(degreeOfParallelism);
		_world.AcquireQueryExecutionScratchForDirectIteration(out var chunkMatches, out var entities, out bool usesSharedScratch);
		try
		{
			int entityCount = _world.FillQueryResultsForQueryEngine(
				handle.Plan,
				chunkMatches,
				_world.QueryResultCapacityForDirectIteration,
				out int chunkMatchCount
			);
			if (entityCount == 0)
				return;

			_world.MaterializeQueryEntities(chunkMatches, chunkMatchCount, entities, entityCount);
			int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
			int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
			_world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
			ParallelWorkScheduler.Execute(
				chunkMatchCount,
				parallelism,
				index =>
				{
					var match = chunkMatches[index];
					if (match.Count == 0)
						return;

					var archetype = _world.GetArchetypeForCursor(match.ArchetypeId);
					int columnIndex1 = GetColumnIndex(archetype, typeId1, match.ArchetypeId);
					int columnIndex2 = GetColumnIndex(archetype, typeId2, match.ArchetypeId);
					var chunk = archetype.GetChunkUnchecked(match.ChunkIndex);
					ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, match.RowStart);
					ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, columnIndex2, match.RowStart);
					var local = job;
					int entityIndex = match.EntityStartIndex;
					for (var offset = 0; offset < match.Count; offset++)
					{
						ref var c1 = ref Unsafe.Add(ref c1Start, offset);
						ref var c2 = ref Unsafe.Add(ref c2Start, offset);
						local.Execute(entities[entityIndex + offset], ref c1, in c2);
					}
				}
			);
		}
		finally
		{
			_world.ReleaseQueryExecutionScratchForDirectIteration(chunkMatches, entities, usesSharedScratch);
		}
	}

	public void RunParallelEntity<TSpec, TJob, T1, T2, T3>(
		QueryHandle<TSpec> handle,
		TJob               job,
		int?               degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		int parallelism = _world.ResolveDegreeOfParallelismForDirectIteration(degreeOfParallelism);
		_world.AcquireQueryExecutionScratchForDirectIteration(out var chunkMatches, out var entities, out bool usesSharedScratch);
		try
		{
			int entityCount = _world.FillQueryResultsForQueryEngine(
				handle.Plan,
				chunkMatches,
				_world.QueryResultCapacityForDirectIteration,
				out int chunkMatchCount
			);
			if (entityCount == 0)
				return;

			_world.MaterializeQueryEntities(chunkMatches, chunkMatchCount, entities, entityCount);
			int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
			int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
			int typeId3 = _world.GetOrCreateComponentTypeId<T3>();
			_world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
			ParallelWorkScheduler.Execute(
				chunkMatchCount,
				parallelism,
				index =>
				{
					var match = chunkMatches[index];
					if (match.Count == 0)
						return;

					var archetype = _world.GetArchetypeForCursor(match.ArchetypeId);
					int columnIndex1 = GetColumnIndex(archetype, typeId1, match.ArchetypeId);
					int columnIndex2 = GetColumnIndex(archetype, typeId2, match.ArchetypeId);
					int columnIndex3 = GetColumnIndex(archetype, typeId3, match.ArchetypeId);
					var chunk = archetype.GetChunkUnchecked(match.ChunkIndex);
					ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, match.RowStart);
					ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, columnIndex2, match.RowStart);
					ref var c3Start = ref archetype.GetRefByIndex<T3>(chunk, columnIndex3, match.RowStart);
					var local = job;
					int entityIndex = match.EntityStartIndex;
					for (var offset = 0; offset < match.Count; offset++)
					{
						ref var c1 = ref Unsafe.Add(ref c1Start, offset);
						ref var c2 = ref Unsafe.Add(ref c2Start, offset);
						ref var c3 = ref Unsafe.Add(ref c3Start, offset);
						local.Execute(entities[entityIndex + offset], ref c1, in c2, in c3);
					}
				}
			);
		}
		finally
		{
			_world.ReleaseQueryExecutionScratchForDirectIteration(chunkMatches, entities, usesSharedScratch);
		}
	}

	public void RunParallelEntity<TSpec, TJob, T1, T2, T3, T4>(
		QueryHandle<TSpec> handle,
		TJob               job,
		int?               degreeOfParallelism = null)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		int parallelism = _world.ResolveDegreeOfParallelismForDirectIteration(degreeOfParallelism);
		_world.AcquireQueryExecutionScratchForDirectIteration(out var chunkMatches, out var entities, out bool usesSharedScratch);
		try
		{
			int entityCount = _world.FillQueryResultsForQueryEngine(
				handle.Plan,
				chunkMatches,
				_world.QueryResultCapacityForDirectIteration,
				out int chunkMatchCount
			);
			if (entityCount == 0)
				return;

			_world.MaterializeQueryEntities(chunkMatches, chunkMatchCount, entities, entityCount);
			int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
			int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
			int typeId3 = _world.GetOrCreateComponentTypeId<T3>();
			int typeId4 = _world.GetOrCreateComponentTypeId<T4>();
			_world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, typeId1);
			ParallelWorkScheduler.Execute(
				chunkMatchCount,
				parallelism,
				index =>
				{
					var match = chunkMatches[index];
					if (match.Count == 0)
						return;

					var archetype = _world.GetArchetypeForCursor(match.ArchetypeId);
					int columnIndex1 = GetColumnIndex(archetype, typeId1, match.ArchetypeId);
					int columnIndex2 = GetColumnIndex(archetype, typeId2, match.ArchetypeId);
					int columnIndex3 = GetColumnIndex(archetype, typeId3, match.ArchetypeId);
					int columnIndex4 = GetColumnIndex(archetype, typeId4, match.ArchetypeId);
					var chunk = archetype.GetChunkUnchecked(match.ChunkIndex);
					ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, match.RowStart);
					ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, columnIndex2, match.RowStart);
					ref var c3Start = ref archetype.GetRefByIndex<T3>(chunk, columnIndex3, match.RowStart);
					ref var c4Start = ref archetype.GetRefByIndex<T4>(chunk, columnIndex4, match.RowStart);
					var local = job;
					int entityIndex = match.EntityStartIndex;
					for (var offset = 0; offset < match.Count; offset++)
					{
						ref var c1 = ref Unsafe.Add(ref c1Start, offset);
						ref var c2 = ref Unsafe.Add(ref c2Start, offset);
						ref var c3 = ref Unsafe.Add(ref c3Start, offset);
						ref var c4 = ref Unsafe.Add(ref c4Start, offset);
						local.Execute(entities[entityIndex + offset], ref c1, in c2, in c3, in c4);
					}
				}
			);
		}
		finally
		{
			_world.ReleaseQueryExecutionScratchForDirectIteration(chunkMatches, entities, usesSharedScratch);
		}
	}

	public void RunDirectFast<TSpec, TJob, T1>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1>
		where T1 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		int matchCount = _world.GetOrRefreshMatchingArchetypesForQueryEngine(handle.Plan);
		if (matchCount == 0)
			return;

		int[] matchingArchetypeIds = handle.Plan.MatchingArchetypeIds;
		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		_world.TrackPotentialDirectFastRefWritesForDirectIteration(matchingArchetypeIds, matchCount, typeId1);
		var processedEntityCount = 0;
		for (var i = 0; i < matchCount; i++)
		{
			int archetypeId = matchingArchetypeIds[i];
			var archetype = _world.GetArchetypeForCursor(archetypeId);
			int columnIndex1 = GetColumnIndex(archetype, typeId1, archetypeId);

			for (var chunkIndex = 0; chunkIndex < archetype.ChunkCount; chunkIndex++)
			{
				var chunk = archetype.GetChunkUnchecked(chunkIndex);
				int chunkCount = chunk.Count;
				if (chunkCount == 0)
					continue;

				int remaining = _world.QueryResultCapacityForDirectIteration - processedEntityCount;
				if (remaining <= 0)
				{
					_world.HandleQueryOverflowForDirectIteration();
					goto done;
				}

				int rowsToProcess = Math.Min(remaining, chunkCount);
				ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, 0);
				for (var offset = 0; offset < rowsToProcess; offset++)
					job.Execute(ref Unsafe.Add(ref c1Start, offset));

				processedEntityCount += rowsToProcess;
				if (rowsToProcess < chunkCount)
				{
					_world.HandleQueryOverflowForDirectIteration();
					goto done;
				}
			}
		}

		done:
		_world.ObserveDirectIterationEntityCount(processedEntityCount);
	}

	public void RunDirectFast<TSpec, TJob, T1, T2>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		int matchCount = _world.GetOrRefreshMatchingArchetypesForQueryEngine(handle.Plan);
		if (matchCount == 0)
			return;

		int[] matchingArchetypeIds = handle.Plan.MatchingArchetypeIds;
		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
		_world.TrackPotentialDirectFastRefWritesForDirectIteration(matchingArchetypeIds, matchCount, typeId1);
		var processedEntityCount = 0;
		for (var i = 0; i < matchCount; i++)
		{
			int archetypeId = matchingArchetypeIds[i];
			var archetype = _world.GetArchetypeForCursor(archetypeId);
			int columnIndex1 = GetColumnIndex(archetype, typeId1, archetypeId);
			int columnIndex2 = GetColumnIndex(archetype, typeId2, archetypeId);

			for (var chunkIndex = 0; chunkIndex < archetype.ChunkCount; chunkIndex++)
			{
				var chunk = archetype.GetChunkUnchecked(chunkIndex);
				int chunkCount = chunk.Count;
				if (chunkCount == 0)
					continue;

				int remaining = _world.QueryResultCapacityForDirectIteration - processedEntityCount;
				if (remaining <= 0)
				{
					_world.HandleQueryOverflowForDirectIteration();
					goto done;
				}

				int rowsToProcess = Math.Min(remaining, chunkCount);
				ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, 0);
				ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, columnIndex2, 0);
				for (var offset = 0; offset < rowsToProcess; offset++)
				{
					ref var c1 = ref Unsafe.Add(ref c1Start, offset);
					ref var c2 = ref Unsafe.Add(ref c2Start, offset);
					job.Execute(ref c1, in c2);
				}

				processedEntityCount += rowsToProcess;
				if (rowsToProcess < chunkCount)
				{
					_world.HandleQueryOverflowForDirectIteration();
					goto done;
				}
			}
		}

		done:
		_world.ObserveDirectIterationEntityCount(processedEntityCount);
	}

	public void RunDirectFast<TSpec, TJob, T1, T2, T3>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		int matchCount = _world.GetOrRefreshMatchingArchetypesForQueryEngine(handle.Plan);
		if (matchCount == 0)
			return;

		int[] matchingArchetypeIds = handle.Plan.MatchingArchetypeIds;
		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
		int typeId3 = _world.GetOrCreateComponentTypeId<T3>();
		_world.TrackPotentialDirectFastRefWritesForDirectIteration(matchingArchetypeIds, matchCount, typeId1);
		var processedEntityCount = 0;
		for (var i = 0; i < matchCount; i++)
		{
			int archetypeId = matchingArchetypeIds[i];
			var archetype = _world.GetArchetypeForCursor(archetypeId);
			int columnIndex1 = GetColumnIndex(archetype, typeId1, archetypeId);
			int columnIndex2 = GetColumnIndex(archetype, typeId2, archetypeId);
			int columnIndex3 = GetColumnIndex(archetype, typeId3, archetypeId);

			for (var chunkIndex = 0; chunkIndex < archetype.ChunkCount; chunkIndex++)
			{
				var chunk = archetype.GetChunkUnchecked(chunkIndex);
				int chunkCount = chunk.Count;
				if (chunkCount == 0)
					continue;

				int remaining = _world.QueryResultCapacityForDirectIteration - processedEntityCount;
				if (remaining <= 0)
				{
					_world.HandleQueryOverflowForDirectIteration();
					goto done;
				}

				int rowsToProcess = Math.Min(remaining, chunkCount);
				ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, columnIndex1, 0);
				ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, columnIndex2, 0);
				ref var c3Start = ref archetype.GetRefByIndex<T3>(chunk, columnIndex3, 0);
				for (var offset = 0; offset < rowsToProcess; offset++)
				{
					ref var c1 = ref Unsafe.Add(ref c1Start, offset);
					ref var c2 = ref Unsafe.Add(ref c2Start, offset);
					ref var c3 = ref Unsafe.Add(ref c3Start, offset);
					job.Execute(ref c1, in c2, in c3);
				}

				processedEntityCount += rowsToProcess;
				if (rowsToProcess < chunkCount)
				{
					_world.HandleQueryOverflowForDirectIteration();
					goto done;
				}
			}
		}

		done:
		_world.ObserveDirectIterationEntityCount(processedEntityCount);
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
