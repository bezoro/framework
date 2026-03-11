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

	private interface IDirectChunkExecutor
	{
		int PrimaryTypeId { get; }
		void PrepareArchetype(ArchetypeStorage archetype, int archetypeId);
		void ExecuteChunk(ArchetypeStorage archetype, ArchetypeStorage.Chunk chunk, int rowStart, int rowCount);
	}

	private interface IDirectEntityChunkExecutor
	{
		int PrimaryTypeId { get; }
		void PrepareArchetype(ArchetypeStorage archetype, int archetypeId);
		void ExecuteChunk(ArchetypeStorage archetype, ArchetypeStorage.Chunk chunk, int rowStart, int rowCount);
	}

	private struct DirectJobExecutor<TJob, T1> : IDirectChunkExecutor
		where TJob : struct, IForEach<T1>
		where T1 : unmanaged
	{
		private readonly int _typeId1;
		private int          _columnIndex1;
		private TJob         _job;

		public DirectJobExecutor(World world, TJob job)
		{
			_typeId1      = world.GetOrCreateComponentTypeId<T1>();
			_columnIndex1 = -1;
			_job          = job;
		}

		public int PrimaryTypeId => _typeId1;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PrepareArchetype(ArchetypeStorage archetype, int archetypeId) =>
			_columnIndex1 = GetColumnIndex(archetype, _typeId1, archetypeId);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ExecuteChunk(ArchetypeStorage archetype, ArchetypeStorage.Chunk chunk, int rowStart, int rowCount)
		{
			ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, _columnIndex1, rowStart);
			for (var offset = 0; offset < rowCount; offset++)
				_job.Execute(ref Unsafe.Add(ref c1Start, offset));
		}
	}

	private struct DirectJobExecutor<TJob, T1, T2> : IDirectChunkExecutor
		where TJob : struct, IForEach<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
	{
		private readonly int _typeId1;
		private readonly int _typeId2;
		private int          _columnIndex1;
		private int          _columnIndex2;
		private TJob         _job;

		public DirectJobExecutor(World world, TJob job)
		{
			_typeId1      = world.GetOrCreateComponentTypeId<T1>();
			_typeId2      = world.GetOrCreateComponentTypeId<T2>();
			_columnIndex1 = -1;
			_columnIndex2 = -1;
			_job          = job;
		}

		public int PrimaryTypeId => _typeId1;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PrepareArchetype(ArchetypeStorage archetype, int archetypeId)
		{
			_columnIndex1 = GetColumnIndex(archetype, _typeId1, archetypeId);
			_columnIndex2 = GetColumnIndex(archetype, _typeId2, archetypeId);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ExecuteChunk(ArchetypeStorage archetype, ArchetypeStorage.Chunk chunk, int rowStart, int rowCount)
		{
			ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, _columnIndex1, rowStart);
			ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, _columnIndex2, rowStart);
			for (var offset = 0; offset < rowCount; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				_job.Execute(ref c1, in c2);
			}
		}
	}

	private struct DirectJobExecutor<TJob, T1, T2, T3> : IDirectChunkExecutor
		where TJob : struct, IForEach<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		private readonly int _typeId1;
		private readonly int _typeId2;
		private readonly int _typeId3;
		private int          _columnIndex1;
		private int          _columnIndex2;
		private int          _columnIndex3;
		private TJob         _job;

		public DirectJobExecutor(World world, TJob job)
		{
			_typeId1      = world.GetOrCreateComponentTypeId<T1>();
			_typeId2      = world.GetOrCreateComponentTypeId<T2>();
			_typeId3      = world.GetOrCreateComponentTypeId<T3>();
			_columnIndex1 = -1;
			_columnIndex2 = -1;
			_columnIndex3 = -1;
			_job          = job;
		}

		public int PrimaryTypeId => _typeId1;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PrepareArchetype(ArchetypeStorage archetype, int archetypeId)
		{
			_columnIndex1 = GetColumnIndex(archetype, _typeId1, archetypeId);
			_columnIndex2 = GetColumnIndex(archetype, _typeId2, archetypeId);
			_columnIndex3 = GetColumnIndex(archetype, _typeId3, archetypeId);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ExecuteChunk(ArchetypeStorage archetype, ArchetypeStorage.Chunk chunk, int rowStart, int rowCount)
		{
			ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, _columnIndex1, rowStart);
			ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, _columnIndex2, rowStart);
			ref var c3Start = ref archetype.GetRefByIndex<T3>(chunk, _columnIndex3, rowStart);
			for (var offset = 0; offset < rowCount; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				ref var c3 = ref Unsafe.Add(ref c3Start, offset);
				_job.Execute(ref c1, in c2, in c3);
			}
		}
	}

	private struct DirectJobExecutor<TJob, T1, T2, T3, T4> : IDirectChunkExecutor
		where TJob : struct, IForEach<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged
	{
		private readonly int _typeId1;
		private readonly int _typeId2;
		private readonly int _typeId3;
		private readonly int _typeId4;
		private int          _columnIndex1;
		private int          _columnIndex2;
		private int          _columnIndex3;
		private int          _columnIndex4;
		private TJob         _job;

		public DirectJobExecutor(World world, TJob job)
		{
			_typeId1      = world.GetOrCreateComponentTypeId<T1>();
			_typeId2      = world.GetOrCreateComponentTypeId<T2>();
			_typeId3      = world.GetOrCreateComponentTypeId<T3>();
			_typeId4      = world.GetOrCreateComponentTypeId<T4>();
			_columnIndex1 = -1;
			_columnIndex2 = -1;
			_columnIndex3 = -1;
			_columnIndex4 = -1;
			_job          = job;
		}

		public int PrimaryTypeId => _typeId1;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PrepareArchetype(ArchetypeStorage archetype, int archetypeId)
		{
			_columnIndex1 = GetColumnIndex(archetype, _typeId1, archetypeId);
			_columnIndex2 = GetColumnIndex(archetype, _typeId2, archetypeId);
			_columnIndex3 = GetColumnIndex(archetype, _typeId3, archetypeId);
			_columnIndex4 = GetColumnIndex(archetype, _typeId4, archetypeId);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ExecuteChunk(ArchetypeStorage archetype, ArchetypeStorage.Chunk chunk, int rowStart, int rowCount)
		{
			ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, _columnIndex1, rowStart);
			ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, _columnIndex2, rowStart);
			ref var c3Start = ref archetype.GetRefByIndex<T3>(chunk, _columnIndex3, rowStart);
			ref var c4Start = ref archetype.GetRefByIndex<T4>(chunk, _columnIndex4, rowStart);
			for (var offset = 0; offset < rowCount; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				ref var c3 = ref Unsafe.Add(ref c3Start, offset);
				ref var c4 = ref Unsafe.Add(ref c4Start, offset);
				_job.Execute(ref c1, in c2, in c3, in c4);
			}
		}
	}

	private struct DirectEntityJobExecutor<TJob, T1> : IDirectEntityChunkExecutor
		where TJob : struct, IForEachEntity<T1>
		where T1 : unmanaged
	{
		private readonly int[] _versions;
		private readonly int   _typeId1;
		private int            _columnIndex1;
		private TJob           _job;

		public DirectEntityJobExecutor(World world, TJob job)
		{
			_versions     = world.GetEntityVersionsForCursor();
			_typeId1      = world.GetOrCreateComponentTypeId<T1>();
			_columnIndex1 = -1;
			_job          = job;
		}

		public int PrimaryTypeId => _typeId1;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PrepareArchetype(ArchetypeStorage archetype, int archetypeId) =>
			_columnIndex1 = GetColumnIndex(archetype, _typeId1, archetypeId);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ExecuteChunk(ArchetypeStorage archetype, ArchetypeStorage.Chunk chunk, int rowStart, int rowCount)
		{
			ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, _columnIndex1, rowStart);
			ref var entityIdStart = ref chunk.EntityIds[rowStart];
			for (var offset = 0; offset < rowCount; offset++)
			{
				int entityId = Unsafe.Add(ref entityIdStart, offset);
				_job.Execute(new(entityId, _versions[entityId]), ref Unsafe.Add(ref c1Start, offset));
			}
		}
	}

	private struct DirectEntityJobExecutor<TJob, T1, T2> : IDirectEntityChunkExecutor
		where TJob : struct, IForEachEntity<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
	{
		private readonly int[] _versions;
		private readonly int   _typeId1;
		private readonly int   _typeId2;
		private int            _columnIndex1;
		private int            _columnIndex2;
		private TJob           _job;

		public DirectEntityJobExecutor(World world, TJob job)
		{
			_versions     = world.GetEntityVersionsForCursor();
			_typeId1      = world.GetOrCreateComponentTypeId<T1>();
			_typeId2      = world.GetOrCreateComponentTypeId<T2>();
			_columnIndex1 = -1;
			_columnIndex2 = -1;
			_job          = job;
		}

		public int PrimaryTypeId => _typeId1;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PrepareArchetype(ArchetypeStorage archetype, int archetypeId)
		{
			_columnIndex1 = GetColumnIndex(archetype, _typeId1, archetypeId);
			_columnIndex2 = GetColumnIndex(archetype, _typeId2, archetypeId);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ExecuteChunk(ArchetypeStorage archetype, ArchetypeStorage.Chunk chunk, int rowStart, int rowCount)
		{
			ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, _columnIndex1, rowStart);
			ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, _columnIndex2, rowStart);
			ref var entityIdStart = ref chunk.EntityIds[rowStart];
			for (var offset = 0; offset < rowCount; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				int entityId = Unsafe.Add(ref entityIdStart, offset);
				_job.Execute(new(entityId, _versions[entityId]), ref c1, in c2);
			}
		}
	}

	private struct DirectEntityJobExecutor<TJob, T1, T2, T3> : IDirectEntityChunkExecutor
		where TJob : struct, IForEachEntity<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		private readonly int[] _versions;
		private readonly int   _typeId1;
		private readonly int   _typeId2;
		private readonly int   _typeId3;
		private int            _columnIndex1;
		private int            _columnIndex2;
		private int            _columnIndex3;
		private TJob           _job;

		public DirectEntityJobExecutor(World world, TJob job)
		{
			_versions     = world.GetEntityVersionsForCursor();
			_typeId1      = world.GetOrCreateComponentTypeId<T1>();
			_typeId2      = world.GetOrCreateComponentTypeId<T2>();
			_typeId3      = world.GetOrCreateComponentTypeId<T3>();
			_columnIndex1 = -1;
			_columnIndex2 = -1;
			_columnIndex3 = -1;
			_job          = job;
		}

		public int PrimaryTypeId => _typeId1;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PrepareArchetype(ArchetypeStorage archetype, int archetypeId)
		{
			_columnIndex1 = GetColumnIndex(archetype, _typeId1, archetypeId);
			_columnIndex2 = GetColumnIndex(archetype, _typeId2, archetypeId);
			_columnIndex3 = GetColumnIndex(archetype, _typeId3, archetypeId);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ExecuteChunk(ArchetypeStorage archetype, ArchetypeStorage.Chunk chunk, int rowStart, int rowCount)
		{
			ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, _columnIndex1, rowStart);
			ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, _columnIndex2, rowStart);
			ref var c3Start = ref archetype.GetRefByIndex<T3>(chunk, _columnIndex3, rowStart);
			ref var entityIdStart = ref chunk.EntityIds[rowStart];
			for (var offset = 0; offset < rowCount; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				ref var c3 = ref Unsafe.Add(ref c3Start, offset);
				int entityId = Unsafe.Add(ref entityIdStart, offset);
				_job.Execute(new(entityId, _versions[entityId]), ref c1, in c2, in c3);
			}
		}
	}

	private struct DirectEntityJobExecutor<TJob, T1, T2, T3, T4> : IDirectEntityChunkExecutor
		where TJob : struct, IForEachEntity<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged
	{
		private readonly int[] _versions;
		private readonly int   _typeId1;
		private readonly int   _typeId2;
		private readonly int   _typeId3;
		private readonly int   _typeId4;
		private int            _columnIndex1;
		private int            _columnIndex2;
		private int            _columnIndex3;
		private int            _columnIndex4;
		private TJob           _job;

		public DirectEntityJobExecutor(World world, TJob job)
		{
			_versions     = world.GetEntityVersionsForCursor();
			_typeId1      = world.GetOrCreateComponentTypeId<T1>();
			_typeId2      = world.GetOrCreateComponentTypeId<T2>();
			_typeId3      = world.GetOrCreateComponentTypeId<T3>();
			_typeId4      = world.GetOrCreateComponentTypeId<T4>();
			_columnIndex1 = -1;
			_columnIndex2 = -1;
			_columnIndex3 = -1;
			_columnIndex4 = -1;
			_job          = job;
		}

		public int PrimaryTypeId => _typeId1;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PrepareArchetype(ArchetypeStorage archetype, int archetypeId)
		{
			_columnIndex1 = GetColumnIndex(archetype, _typeId1, archetypeId);
			_columnIndex2 = GetColumnIndex(archetype, _typeId2, archetypeId);
			_columnIndex3 = GetColumnIndex(archetype, _typeId3, archetypeId);
			_columnIndex4 = GetColumnIndex(archetype, _typeId4, archetypeId);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ExecuteChunk(ArchetypeStorage archetype, ArchetypeStorage.Chunk chunk, int rowStart, int rowCount)
		{
			ref var c1Start = ref archetype.GetRefByIndex<T1>(chunk, _columnIndex1, rowStart);
			ref var c2Start = ref archetype.GetRefByIndex<T2>(chunk, _columnIndex2, rowStart);
			ref var c3Start = ref archetype.GetRefByIndex<T3>(chunk, _columnIndex3, rowStart);
			ref var c4Start = ref archetype.GetRefByIndex<T4>(chunk, _columnIndex4, rowStart);
			ref var entityIdStart = ref chunk.EntityIds[rowStart];
			for (var offset = 0; offset < rowCount; offset++)
			{
				ref var c1 = ref Unsafe.Add(ref c1Start, offset);
				ref var c2 = ref Unsafe.Add(ref c2Start, offset);
				ref var c3 = ref Unsafe.Add(ref c3Start, offset);
				ref var c4 = ref Unsafe.Add(ref c4Start, offset);
				int entityId = Unsafe.Add(ref entityIdStart, offset);
				_job.Execute(new(entityId, _versions[entityId]), ref c1, in c2, in c3, in c4);
			}
		}
	}

	public void RunDirectFast<TSpec, TJob, T1>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1>
		where T1 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		var executor = new DirectJobExecutor<TJob, T1>(_world, job);
		ExecuteDirectValidated(handle, ref executor);
	}

	public void RunDirectFast<TSpec, TJob, T1, T2>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		var executor = new DirectJobExecutor<TJob, T1, T2>(_world, job);
		ExecuteDirectValidated(handle, ref executor);
	}

	public void RunDirectFast<TSpec, TJob, T1, T2, T3>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		var executor = new DirectJobExecutor<TJob, T1, T2, T3>(_world, job);
		ExecuteDirectValidated(handle, ref executor);
	}

	public void RunDirectFast<TSpec, TJob, T1, T2, T3, T4>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		var executor = new DirectJobExecutor<TJob, T1, T2, T3, T4>(_world, job);
		ExecuteDirectValidated(handle, ref executor);
	}

	public void RunDirectFastEntity<TSpec, TJob, T1>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1>
		where T1 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		var executor = new DirectEntityJobExecutor<TJob, T1>(_world, job);
		ExecuteDirectEntityValidated(handle, ref executor);
	}

	public void RunDirectFastEntity<TSpec, TJob, T1, T2>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		var executor = new DirectEntityJobExecutor<TJob, T1, T2>(_world, job);
		ExecuteDirectEntityValidated(handle, ref executor);
	}

	public void RunDirectFastEntity<TSpec, TJob, T1, T2, T3>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		var executor = new DirectEntityJobExecutor<TJob, T1, T2, T3>(_world, job);
		ExecuteDirectEntityValidated(handle, ref executor);
	}

	public void RunDirectFastEntity<TSpec, TJob, T1, T2, T3, T4>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEachEntity<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
		where T4 : unmanaged
	{
		_world.ValidateDirectIterationHandle(handle);
		var executor = new DirectEntityJobExecutor<TJob, T1, T2, T3, T4>(_world, job);
		ExecuteDirectEntityValidated(handle, ref executor);
	}

	public void ExecuteDirectEntityAction<TSpec, TAction, T1>(QueryHandle<TSpec> handle, TAction action)
		where TSpec : struct, ICompiledQuerySpec
		where TAction : struct, IEntityChunkAction<T1>
		where T1 : struct
	{
		_world.ValidateDirectIterationHandle(handle);
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

			QueryChunkWalker.ExecuteEntity<TAction, T1>(_world, chunkMatches, chunkMatchCount, action);
		}
		finally
		{
			_world.ReleaseQueryChunkMatchScratchForDirectIteration(chunkMatches, usesSharedScratch);
		}
	}

	public void ExecuteDirectEntityAction<TSpec, TAction, T1, T2>(QueryHandle<TSpec> handle, TAction action)
		where TSpec : struct, ICompiledQuerySpec
		where TAction : struct, IEntityChunkAction<T1, T2>
		where T1 : struct
		where T2 : struct
	{
		_world.ValidateDirectIterationHandle(handle);
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

			QueryChunkWalker.ExecuteEntity<TAction, T1, T2>(_world, chunkMatches, chunkMatchCount, action);
		}
		finally
		{
			_world.ReleaseQueryChunkMatchScratchForDirectIteration(chunkMatches, usesSharedScratch);
		}
	}

	public void ExecuteDirectEntityAction<TSpec, TAction, T1, T2, T3>(QueryHandle<TSpec> handle, TAction action)
		where TSpec : struct, ICompiledQuerySpec
		where TAction : struct, IEntityChunkAction<T1, T2, T3>
		where T1 : struct
		where T2 : struct
		where T3 : struct
	{
		_world.ValidateDirectIterationHandle(handle);
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

			QueryChunkWalker.ExecuteEntity<TAction, T1, T2, T3>(_world, chunkMatches, chunkMatchCount, action);
		}
		finally
		{
			_world.ReleaseQueryChunkMatchScratchForDirectIteration(chunkMatches, usesSharedScratch);
		}
	}

	public void ExecuteDirectEntityAction<TSpec, TAction, T1, T2, T3, T4>(QueryHandle<TSpec> handle, TAction action)
		where TSpec : struct, ICompiledQuerySpec
		where TAction : struct, IEntityChunkAction<T1, T2, T3, T4>
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct
	{
		_world.ValidateDirectIterationHandle(handle);
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

			QueryChunkWalker.ExecuteEntity<TAction, T1, T2, T3, T4>(_world, chunkMatches, chunkMatchCount, action);
		}
		finally
		{
			_world.ReleaseQueryChunkMatchScratchForDirectIteration(chunkMatches, usesSharedScratch);
		}
	}

	private void ExecuteDirectValidated<TSpec, TExecutor>(QueryHandle<TSpec> handle, ref TExecutor executor)
		where TSpec : struct, ICompiledQuerySpec
		where TExecutor : struct, IDirectChunkExecutor
	{
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

			int cachedArchetypeId = int.MinValue;
			ArchetypeStorage? cachedArchetype = null;
			_world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, executor.PrimaryTypeId);
			for (var i = 0; i < chunkMatchCount; i++)
			{
				var match = chunkMatches[i];
				if (match.Count == 0)
					continue;

				if (match.ArchetypeId != cachedArchetypeId)
				{
					cachedArchetypeId = match.ArchetypeId;
					cachedArchetype = _world.GetArchetypeForCursor(match.ArchetypeId);
					executor.PrepareArchetype(cachedArchetype, match.ArchetypeId);
				}

				var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
				executor.ExecuteChunk(cachedArchetype, chunk, match.RowStart, match.Count);
			}
		}
		finally
		{
			_world.ReleaseQueryChunkMatchScratchForDirectIteration(chunkMatches, usesSharedScratch);
		}
	}

	private void ExecuteDirectEntityValidated<TSpec, TExecutor>(QueryHandle<TSpec> handle, ref TExecutor executor)
		where TSpec : struct, ICompiledQuerySpec
		where TExecutor : struct, IDirectEntityChunkExecutor
	{
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

			int cachedArchetypeId = int.MinValue;
			ArchetypeStorage? cachedArchetype = null;
			_world.TrackPotentialChunkMatchRefWrites(chunkMatches, chunkMatchCount, executor.PrimaryTypeId);
			for (var i = 0; i < chunkMatchCount; i++)
			{
				var match = chunkMatches[i];
				if (match.Count == 0)
					continue;

				if (match.ArchetypeId != cachedArchetypeId)
				{
					cachedArchetypeId = match.ArchetypeId;
					cachedArchetype = _world.GetArchetypeForCursor(match.ArchetypeId);
					executor.PrepareArchetype(cachedArchetype, match.ArchetypeId);
				}

				var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
				executor.ExecuteChunk(cachedArchetype, chunk, match.RowStart, match.Count);
			}
		}
		finally
		{
			_world.ReleaseQueryChunkMatchScratchForDirectIteration(chunkMatches, usesSharedScratch);
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
