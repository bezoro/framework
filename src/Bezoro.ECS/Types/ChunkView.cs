using Bezoro.ECS.Internal;
using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
///     Provides access to a chunk of entities and component columns.
/// </summary>
public readonly struct ChunkView
{
	private readonly bool              _trackWrites;
	private readonly Chunk?            _chunk;
	private readonly ComponentColumn[] _columns;
	private readonly Entity[]          _entities;
	private readonly int[]             _typeIndexById;
	private readonly uint              _currentVersion;
	private readonly uint[]            _componentVersions;
	private readonly WorldV1             _world;

	internal ChunkView(
		Entity[]          entities,
		ComponentColumn[] columns,
		int               count,
		int[]             typeIndexById,
		uint[]            componentVersions,
		uint              currentVersion,
		bool              trackWrites,
		Chunk?            chunk,
		WorldV1             world)
	{
		_entities          = entities;
		_columns           = columns;
		Count              = count;
		_typeIndexById     = typeIndexById;
		_componentVersions = componentVersions;
		_currentVersion    = currentVersion;
		_trackWrites       = trackWrites;
		_chunk             = chunk;
		_world             = world;
	}

	public int Count { get; }

	public ReadOnlySpan<Entity> Entities => new(_entities, 0, Count);

	public bool TryComponents<T>(out Span<T> components) where T : struct
	{
		int typeId = _world.GetOrCreateComponentTypeId<T>();
		return TryComponentsByTypeId(typeId, out components);
	}

	public ReadOnlySpan<T> ReadOnlyComponents<T>() where T : struct
	{
		int typeId = _world.GetOrCreateComponentTypeId<T>();
		return ReadOnlyComponentsByTypeId<T>(typeId);
	}

	public Span<T> Components<T>() where T : struct
	{
		int typeId = _world.GetOrCreateComponentTypeId<T>();
		return ComponentsByTypeId<T>(typeId);
	}

	public Span<T> OptionalComponents<T>() where T : struct
	{
		int typeId = _world.GetOrCreateComponentTypeId<T>();
		return OptionalComponentsByTypeId<T>(typeId);
	}

	internal bool TryComponentsByTypeId<T>(int typeId, out Span<T> components) where T : struct
	{
		int index  = GetIndex(typeId);
		if (index < 0)
		{
			components = default;
			return false;
		}

		components = _columns[index].GetSpan<T>(Count);
		return true;
	}

	internal ReadOnlySpan<T> ReadOnlyComponentsByTypeId<T>(int typeId) where T : struct
	{
		int index  = GetIndex(typeId);
		if (index < 0)
			throw new KeyNotFoundException($"Component of type {typeof(T).Name} not found in chunk.");

		return _columns[index].GetReadOnlySpan<T>(Count);
	}

	internal Span<T> ComponentsByTypeId<T>(int typeId) where T : struct
	{
		int index  = GetIndex(typeId);
		if (index < 0)
			throw new KeyNotFoundException($"Component of type {typeof(T).Name} not found in chunk.");

		if (_trackWrites)
			MarkChanged(index);

		return _columns[index].GetSpan<T>(Count);
	}

	internal Span<T> OptionalComponentsByTypeId<T>(int typeId) where T : struct
	{
		int index  = GetIndex(typeId);
		if (index < 0)
			return Span<T>.Empty;

		if (_trackWrites)
			MarkChanged(index);

		return _columns[index].GetSpan<T>(Count);
	}

	internal bool IsChanged(int typeId)
	{
		int index = GetIndex(typeId);
		if (index < 0)
			return false;

		return _componentVersions[index] == _currentVersion;
	}

	private int GetIndex(int typeId)
	{
		if (typeId < 0 || typeId >= _typeIndexById.Length) return -1;

		return _typeIndexById[typeId];
	}

	private void MarkChanged(int index)
	{
		if (_chunk is null) return;

		_chunk.MarkChanged(index, _currentVersion);
	}
}
