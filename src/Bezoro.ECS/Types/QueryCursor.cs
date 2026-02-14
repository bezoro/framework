using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal.V2;
using Bezoro.ECS.Services;
using System.Runtime.CompilerServices;

namespace Bezoro.ECS.Types;

/// <summary>
/// Cursor over one compiled-query execution result set.
/// </summary>
public struct QueryCursor : IDisposable
{
	/// <summary>
	/// Delegate invoked with one mutable unmanaged component.
	/// </summary>
	public delegate void RefAction<T1>(ref T1 component1) where T1 : unmanaged;

	/// <summary>
	/// Delegate invoked with one mutable and one read-only unmanaged component.
	/// </summary>
	public delegate void RefInAction<T1, T2>(ref T1 component1, in T2 component2)
		where T1 : unmanaged
		where T2 : unmanaged;

	/// <summary>
	/// Delegate invoked with one mutable and two read-only unmanaged components.
	/// </summary>
	public delegate void RefInAction<T1, T2, T3>(ref T1 component1, in T2 component2, in T3 component3)
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged;

	private readonly WorldV2          _world;
	private readonly QueryChunkMatch[] _chunkMatches;
	private readonly int               _chunkMatchCount;
	private readonly Entity[]          _entities;
	private readonly int               _count;
	private          bool              _disposed;
	private          bool              _moved;
	private          bool              _entitiesMaterialized;
	private          int               _chunkMatchHint;

	internal QueryCursor(
		WorldV2           world,
		QueryChunkMatch[] chunkMatches,
		int               chunkMatchCount,
		Entity[]          entities,
		int               count
	)
	{
		_world = world;
		_chunkMatches = chunkMatches;
		_chunkMatchCount = chunkMatchCount;
		_entities = entities;
		_count = count;
		_disposed = false;
		_moved = false;
		_entitiesMaterialized = false;
		_chunkMatchHint = -1;
	}

	/// <summary>
	/// Advances to the only available batch.
	/// </summary>
	/// <returns><c>true</c> on first call; otherwise <c>false</c>.</returns>
	public bool MoveNext()
	{
		ThrowIfDisposed();
		if (_moved)
			return false;

		_moved = true;
		return true;
	}

	/// <summary>
	/// Current matched entities.
	/// </summary>
	public ReadOnlySpan<Entity> Current
	{
		get
		{
			ThrowIfDisposed();
			if (!_moved)
				return ReadOnlySpan<Entity>.Empty;

			EnsureEntitiesMaterialized();
			return new(_entities, 0, _count);
		}
	}

	/// <summary>
	/// Gets a mutable unmanaged component reference for the matched entity at <paramref name="index" />.
	/// </summary>
	/// <typeparam name="T">Unmanaged component type.</typeparam>
	/// <param name="index">Entity index in the current batch.</param>
	/// <returns>Mutable component reference.</returns>
	public ref T Get<T>(int index) where T : unmanaged
	{
		ThrowIfDisposed();
		if (!_moved)
			throw new InvalidOperationException("Call MoveNext before accessing components.");

		if ((uint)index >= (uint)_count)
			throw new ArgumentOutOfRangeException(nameof(index));

		int entityId = _world.ResolveEntityIdForCursorIndex(
			_chunkMatches,
			_chunkMatchCount,
			index,
			ref _chunkMatchHint
		);
		int typeId = _world.GetOrCreateComponentTypeId<T>();
		return ref _world.GetComponentRefForCursorUnchecked<T>(entityId, typeId);
	}

	/// <summary>
	/// Executes a no-allocation sequential loop over one mutable unmanaged component.
	/// </summary>
	/// <typeparam name="T1">Mutable unmanaged component type.</typeparam>
	/// <param name="action">Per-entity callback.</param>
	public void ForEach<T1>(RefAction<T1> action) where T1 : unmanaged
	{
		ThrowIfDisposed();
		if (action is null) throw new ArgumentNullException(nameof(action));
		if (!_moved)
			throw new InvalidOperationException("Call MoveNext before enumerating components.");

		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		var cachedArchetypeId = int.MinValue;
		ArchetypeStorage? cachedArchetype = null;
		var cachedColumnIndex1 = -1;
		for (var i = 0; i < _chunkMatchCount; i++)
		{
			var match = _chunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId = match.ArchetypeId;
				cachedArchetype = _world.GetArchetypeForCursor(match.ArchetypeId);
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException($"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'.");
			}

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			if (match.Count == 0)
				continue;

			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			for (var offset = 0; offset < match.Count; offset++)
				action(ref Unsafe.Add(ref c1Start, offset));
		}
	}

	/// <summary>
	/// Executes a no-allocation sequential struct job over one mutable unmanaged component.
	/// </summary>
	/// <typeparam name="TJob">Job type implementing <see cref="IForEach{T1}" />.</typeparam>
	/// <typeparam name="T1">Mutable unmanaged component type.</typeparam>
	/// <param name="job">Job instance.</param>
	public void Run<TJob, T1>(TJob job)
		where TJob : struct, IForEach<T1>
		where T1 : unmanaged
	{
		ThrowIfDisposed();
		if (!_moved)
			throw new InvalidOperationException("Call MoveNext before enumerating components.");

		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		var cachedArchetypeId = int.MinValue;
		ArchetypeStorage? cachedArchetype = null;
		var cachedColumnIndex1 = -1;
		for (var i = 0; i < _chunkMatchCount; i++)
		{
			var match = _chunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId = match.ArchetypeId;
				cachedArchetype = _world.GetArchetypeForCursor(match.ArchetypeId);
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException($"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'.");
			}

			var chunk = cachedArchetype!.GetChunkUnchecked(match.ChunkIndex);
			if (match.Count == 0)
				continue;

			ref var c1Start = ref cachedArchetype.GetRefByIndex<T1>(chunk, cachedColumnIndex1, match.RowStart);
			for (var offset = 0; offset < match.Count; offset++)
				job.Execute(ref Unsafe.Add(ref c1Start, offset));
		}
	}

	/// <summary>
	/// Executes a no-allocation sequential loop over one mutable and one read-only unmanaged component.
	/// </summary>
	/// <typeparam name="T1">Mutable unmanaged component type.</typeparam>
	/// <typeparam name="T2">Read-only unmanaged component type.</typeparam>
	/// <param name="action">Per-entity callback.</param>
	public void ForEach<T1, T2>(RefInAction<T1, T2> action)
		where T1 : unmanaged
		where T2 : unmanaged
	{
		ThrowIfDisposed();
		if (action is null) throw new ArgumentNullException(nameof(action));
		if (!_moved)
			throw new InvalidOperationException("Call MoveNext before enumerating components.");

		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
		var cachedArchetypeId = int.MinValue;
		ArchetypeStorage? cachedArchetype = null;
		var cachedColumnIndex1 = -1;
		var cachedColumnIndex2 = -1;
		for (var i = 0; i < _chunkMatchCount; i++)
		{
			var match = _chunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId = match.ArchetypeId;
				cachedArchetype = _world.GetArchetypeForCursor(match.ArchetypeId);
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException($"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'.");
				cachedColumnIndex2 = cachedArchetype.GetColumnIndexOrNegative(typeId2);
				if (cachedColumnIndex2 < 0)
					throw new KeyNotFoundException($"Type id '{typeId2}' does not exist in archetype '{match.ArchetypeId}'.");
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
				action(ref c1, in c2);
			}
		}
	}

	/// <summary>
	/// Executes a no-allocation sequential struct job over one mutable and one read-only unmanaged component.
	/// </summary>
	/// <typeparam name="TJob">Job type implementing <see cref="IForEach{T1, T2}" />.</typeparam>
	/// <typeparam name="T1">Mutable unmanaged component type.</typeparam>
	/// <typeparam name="T2">Read-only unmanaged component type.</typeparam>
	/// <param name="job">Job instance.</param>
	public void Run<TJob, T1, T2>(TJob job)
		where TJob : struct, IForEach<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
	{
		ThrowIfDisposed();
		if (!_moved)
			throw new InvalidOperationException("Call MoveNext before enumerating components.");

		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
		var cachedArchetypeId = int.MinValue;
		ArchetypeStorage? cachedArchetype = null;
		var cachedColumnIndex1 = -1;
		var cachedColumnIndex2 = -1;
		for (var i = 0; i < _chunkMatchCount; i++)
		{
			var match = _chunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId = match.ArchetypeId;
				cachedArchetype = _world.GetArchetypeForCursor(match.ArchetypeId);
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException($"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'.");
				cachedColumnIndex2 = cachedArchetype.GetColumnIndexOrNegative(typeId2);
				if (cachedColumnIndex2 < 0)
					throw new KeyNotFoundException($"Type id '{typeId2}' does not exist in archetype '{match.ArchetypeId}'.");
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
				job.Execute(ref c1, in c2);
			}
		}
	}

	/// <summary>
	/// Executes a no-allocation sequential loop over one mutable and two read-only unmanaged components.
	/// </summary>
	/// <typeparam name="T1">Mutable unmanaged component type.</typeparam>
	/// <typeparam name="T2">First read-only unmanaged component type.</typeparam>
	/// <typeparam name="T3">Second read-only unmanaged component type.</typeparam>
	/// <param name="action">Per-entity callback.</param>
	public void ForEach<T1, T2, T3>(RefInAction<T1, T2, T3> action)
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		ThrowIfDisposed();
		if (action is null) throw new ArgumentNullException(nameof(action));
		if (!_moved)
			throw new InvalidOperationException("Call MoveNext before enumerating components.");

		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
		int typeId3 = _world.GetOrCreateComponentTypeId<T3>();
		var cachedArchetypeId = int.MinValue;
		ArchetypeStorage? cachedArchetype = null;
		var cachedColumnIndex1 = -1;
		var cachedColumnIndex2 = -1;
		var cachedColumnIndex3 = -1;
		for (var i = 0; i < _chunkMatchCount; i++)
		{
			var match = _chunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId = match.ArchetypeId;
				cachedArchetype = _world.GetArchetypeForCursor(match.ArchetypeId);
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException($"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'.");
				cachedColumnIndex2 = cachedArchetype.GetColumnIndexOrNegative(typeId2);
				if (cachedColumnIndex2 < 0)
					throw new KeyNotFoundException($"Type id '{typeId2}' does not exist in archetype '{match.ArchetypeId}'.");
				cachedColumnIndex3 = cachedArchetype.GetColumnIndexOrNegative(typeId3);
				if (cachedColumnIndex3 < 0)
					throw new KeyNotFoundException($"Type id '{typeId3}' does not exist in archetype '{match.ArchetypeId}'.");
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
				action(ref c1, in c2, in c3);
			}
		}
	}

	/// <summary>
	/// Executes a no-allocation sequential struct job over one mutable and two read-only unmanaged components.
	/// </summary>
	/// <typeparam name="TJob">Job type implementing <see cref="IForEach{T1, T2, T3}" />.</typeparam>
	/// <typeparam name="T1">Mutable unmanaged component type.</typeparam>
	/// <typeparam name="T2">First read-only unmanaged component type.</typeparam>
	/// <typeparam name="T3">Second read-only unmanaged component type.</typeparam>
	/// <param name="job">Job instance.</param>
	public void Run<TJob, T1, T2, T3>(TJob job)
		where TJob : struct, IForEach<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		ThrowIfDisposed();
		if (!_moved)
			throw new InvalidOperationException("Call MoveNext before enumerating components.");

		int typeId1 = _world.GetOrCreateComponentTypeId<T1>();
		int typeId2 = _world.GetOrCreateComponentTypeId<T2>();
		int typeId3 = _world.GetOrCreateComponentTypeId<T3>();
		var cachedArchetypeId = int.MinValue;
		ArchetypeStorage? cachedArchetype = null;
		var cachedColumnIndex1 = -1;
		var cachedColumnIndex2 = -1;
		var cachedColumnIndex3 = -1;
		for (var i = 0; i < _chunkMatchCount; i++)
		{
			var match = _chunkMatches[i];
			if (match.ArchetypeId != cachedArchetypeId)
			{
				cachedArchetypeId = match.ArchetypeId;
				cachedArchetype = _world.GetArchetypeForCursor(match.ArchetypeId);
				cachedColumnIndex1 = cachedArchetype.GetColumnIndexOrNegative(typeId1);
				if (cachedColumnIndex1 < 0)
					throw new KeyNotFoundException($"Type id '{typeId1}' does not exist in archetype '{match.ArchetypeId}'.");
				cachedColumnIndex2 = cachedArchetype.GetColumnIndexOrNegative(typeId2);
				if (cachedColumnIndex2 < 0)
					throw new KeyNotFoundException($"Type id '{typeId2}' does not exist in archetype '{match.ArchetypeId}'.");
				cachedColumnIndex3 = cachedArchetype.GetColumnIndexOrNegative(typeId3);
				if (cachedColumnIndex3 < 0)
					throw new KeyNotFoundException($"Type id '{typeId3}' does not exist in archetype '{match.ArchetypeId}'.");
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
				job.Execute(ref c1, in c2, in c3);
			}
		}
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		_world.ExitQueryCursor();
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(QueryCursor));
	}

	private void EnsureEntitiesMaterialized()
	{
		if (_entitiesMaterialized)
			return;

		_world.MaterializeQueryEntities(_chunkMatches, _chunkMatchCount, _entities, _count);
		_entitiesMaterialized = true;
	}
}
