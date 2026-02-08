using System.Collections.Concurrent;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class ComponentTypeRegistry
{
	private readonly ConcurrentDictionary<(Type relationType, int targetId, int targetVersion), int> _relationToId = new();
	private readonly ConcurrentDictionary<int, RelationshipInfo>                                     _relationshipInfoById = new();
	private readonly ConcurrentDictionary<Type, int>                                                 _typeToId = new();
	private readonly ConcurrentDictionary<Type, List<int>>                                           _relationIdsByType = new();
	private readonly object                                                                          _sync = new();

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

			int id = _idToTypeCount;
			EnsureIdToTypeCapacity(id + 1);
			_idToTypeArray[id] = typeof(RelationMarker);
			Volatile.Write(ref _idToTypeCount, id + 1);

			_relationToId[key]            = id;
			_relationshipInfoById[id]     = new(relationType, target);

			var ids = _relationIdsByType.GetOrAdd(relationType, _ => []);
			lock (ids)
			{
				ids.Add(id);
			}

			return id;
		}
	}

	public int[] GetRelationshipIds(Type relationType)
	{
		if (relationType is null) throw new ArgumentNullException(nameof(relationType));

		if (!_relationIdsByType.TryGetValue(relationType, out var ids))
			return [];

		lock (ids)
		{
			return [.. ids];
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
}
