using System.Collections.Concurrent;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class ComponentTypeRegistry
{
	private readonly ConcurrentDictionary<(Type relationType, int targetId, int targetVersion), int> _relationToId = new();
	private readonly ConcurrentDictionary<int, (Type relationType, int targetId, int targetVersion)> _relationKeyById = new();
	private readonly ConcurrentDictionary<(int targetId, int targetVersion), int[]> _relationIdsByTarget = new();
	private readonly ConcurrentDictionary<int, RelationshipInfo>                     _relationshipInfoById = new();
	private readonly ConcurrentDictionary<Type, int>                                 _typeToId = new();
	private readonly ConcurrentDictionary<Type, int[]>                               _relationIdsByType = new();
	private readonly object                                                          _sync = new();
	private readonly Stack<int>                                                      _recycledRelationshipTypeIds = new();

	private Type[] _idToTypeArray = new Type[16];
	private int    _idToTypeCount;

	public int Count => Volatile.Read(ref _idToTypeCount);

	public bool IsRelationship(int id) => _relationshipInfoById.ContainsKey(id);

	public bool TryGetRelationshipInfo(int id, out RelationshipInfo info) =>
		_relationshipInfoById.TryGetValue(id, out info);

	public int GetOrCreate<T>() where T : struct =>
		GetOrCreate(typeof(T));

	public int GetOrCreate(Type type)
	{
		if (type is null) throw new ArgumentNullException(nameof(type));

		if (!type.IsValueType)
			throw new ArgumentException("Component types must be value types.", nameof(type));

		if (_typeToId.TryGetValue(type, out int existing))
			return existing;

		lock (_sync)
		{
			if (_typeToId.TryGetValue(type, out existing))
				return existing;

			int id = _idToTypeCount;
			EnsureIdToTypeCapacity(id + 1);
			_idToTypeArray[id] = type;
			Volatile.Write(ref _idToTypeCount, id + 1);
			_typeToId[type] = id;
			return id;
		}
	}

	public int GetOrCreateRelationship(Type relationType, Entity target)
	{
		if (relationType is null) throw new ArgumentNullException(nameof(relationType));

		var key = (relationType, target.Id, target.Version);
		if (_relationToId.TryGetValue(key, out int existing))
			return existing;

		lock (_sync)
		{
			if (_relationToId.TryGetValue(key, out existing))
				return existing;

			int id;
			if (_recycledRelationshipTypeIds.Count > 0)
			{
				id = _recycledRelationshipTypeIds.Pop();
				_idToTypeArray[id] = typeof(RelationMarker);
			}
			else
			{
				id = _idToTypeCount;
				EnsureIdToTypeCapacity(id + 1);
				_idToTypeArray[id] = typeof(RelationMarker);
				Volatile.Write(ref _idToTypeCount, id + 1);
			}

			_relationToId[key]            = id;
			_relationKeyById[id]          = key;
			_relationshipInfoById[id]     = new(relationType, target);
			AppendId(_relationIdsByType, relationType, id);
			AppendId(_relationIdsByTarget, (target.Id, target.Version), id);

			return id;
		}
	}

	public ReadOnlySpan<int> GetRelationshipIds(Type relationType)
	{
		if (relationType is null) throw new ArgumentNullException(nameof(relationType));

		return _relationIdsByType.TryGetValue(relationType, out var ids) ? ids : [];
	}

	public ReadOnlySpan<int> GetRelationshipIdsForTarget(Entity target) =>
		_relationIdsByTarget.TryGetValue((target.Id, target.Version), out var ids) ? ids : [];

	public void ReleaseRelationshipsForTarget(Entity target)
	{
		var targetKey = (target.Id, target.Version);
		if (!_relationIdsByTarget.TryGetValue(targetKey, out var ids) || ids.Length == 0)
			return;

		lock (_sync)
		{
			if (!_relationIdsByTarget.TryRemove(targetKey, out ids) || ids.Length == 0)
				return;

			for (var i = 0; i < ids.Length; i++)
			{
				int id = ids[i];
				if (!_relationshipInfoById.TryRemove(id, out var info))
					continue;

				if (_relationKeyById.TryRemove(id, out var relationKey))
					_relationToId.TryRemove(relationKey, out _);

				RemoveId(_relationIdsByType, info.RelationType, id);
				_recycledRelationshipTypeIds.Push(id);
			}
		}
	}

	public void ClearRelationships()
	{
		lock (_sync)
		{
			_relationToId.Clear();
			_relationKeyById.Clear();
			_relationshipInfoById.Clear();
			_relationIdsByType.Clear();
			_relationIdsByTarget.Clear();
			_recycledRelationshipTypeIds.Clear();

			for (var id = 0; id < _idToTypeCount; id++)
			{
				if (_idToTypeArray[id] == typeof(RelationMarker))
					_recycledRelationshipTypeIds.Push(id);
			}
		}
	}

	public Type GetType(int id)
	{
		int count = Volatile.Read(ref _idToTypeCount);
		if ((uint)id >= (uint)count)
			throw new ArgumentOutOfRangeException(nameof(id));

		return _idToTypeArray[id];
	}

	private void EnsureIdToTypeCapacity(int required)
	{
		if (required <= _idToTypeArray.Length) return;

		int newCapacity = _idToTypeArray.Length;
		while (newCapacity < required)
			newCapacity *= 2;

		var newArray = new Type[newCapacity];
		Array.Copy(_idToTypeArray, newArray, _idToTypeCount);
		_idToTypeArray = newArray;
	}

	private static void AppendId<TKey>(ConcurrentDictionary<TKey, int[]> map, TKey key, int id) where TKey : notnull
	{
		var current = map.TryGetValue(key, out var ids) ? ids : [];
		var updated = new int[current.Length + 1];
		Array.Copy(current, updated, current.Length);
		updated[^1] = id;
		map[key] = updated;
	}

	private static void RemoveId<TKey>(ConcurrentDictionary<TKey, int[]> map, TKey key, int id) where TKey : notnull
	{
		if (!map.TryGetValue(key, out var ids) || ids.Length == 0)
			return;

		var index = -1;
		for (var i = 0; i < ids.Length; i++)
		{
			if (ids[i] != id) continue;

			index = i;
			break;
		}

		if (index < 0)
			return;

		if (ids.Length == 1)
		{
			map.TryRemove(key, out _);
			return;
		}

		var updated = new int[ids.Length - 1];
		if (index > 0)
			Array.Copy(ids, 0, updated, 0, index);
		if (index < ids.Length - 1)
			Array.Copy(ids, index + 1, updated, index, ids.Length - index - 1);
		map[key] = updated;
	}
}
