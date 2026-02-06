using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal static class ComponentTypeRegistry
{
	private static readonly object _sync = new();
	private static readonly Dictionary<Type, int> _typeToId = new();
	private static readonly List<Type> _idToType = [];
	private static readonly Dictionary<(Type relationType, int targetId, int targetVersion, int targetWorld), int> _relationToId = new();
	private static readonly Dictionary<int, RelationshipInfo> _relationshipInfoById = [];
	private static readonly Dictionary<Type, List<int>> _relationIdsByType = [];

	public static int Count
	{
		get
		{
			lock (_sync)
			{
				return _idToType.Count;
			}
		}
	}

	public static int GetOrCreate<T>() where T : struct, IComponent =>
		GetOrCreate(typeof(T));

	public static int GetOrCreate(Type type)
	{
		if (type is null) throw new ArgumentNullException(nameof(type));

		if (!type.IsValueType || !typeof(IComponent).IsAssignableFrom(type))
			throw new ArgumentException("Component types must be structs implementing IComponent.", nameof(type));

		lock (_sync)
		{
			if (_typeToId.TryGetValue(type, out int id))
				return id;

			id = _idToType.Count;
			_typeToId[type] = id;
			_idToType.Add(type);
			return id;
		}
	}

	public static int GetOrCreateRelationship(Type relationType, Entity target)
	{
		if (relationType is null) throw new ArgumentNullException(nameof(relationType));

		var key = (relationType, target.Id, target.Version, target.WorldId);
		lock (_sync)
		{
			if (_relationToId.TryGetValue(key, out int id))
				return id;

			id = _idToType.Count;
			_relationToId[key] = id;
			_idToType.Add(typeof(RelationMarker));
			_relationshipInfoById[id] = new(relationType, target);
			if (!_relationIdsByType.TryGetValue(relationType, out var ids))
			{
				ids = [];
				_relationIdsByType[relationType] = ids;
			}

			ids.Add(id);
			return id;
		}
	}

	public static bool IsRelationship(int id)
	{
		lock (_sync)
		{
			return _relationshipInfoById.ContainsKey(id);
		}
	}

	public static bool TryGetRelationshipInfo(int id, out RelationshipInfo info)
	{
		lock (_sync)
		{
			return _relationshipInfoById.TryGetValue(id, out info);
		}
	}

	public static int[] GetRelationshipIds(Type relationType)
	{
		if (relationType is null) throw new ArgumentNullException(nameof(relationType));

		lock (_sync)
		{
			if (!_relationIdsByType.TryGetValue(relationType, out var ids))
				return [];

			return [.. ids];
		}
	}

	public static Type GetType(int id)
	{
		lock (_sync)
		{
			return _idToType[id];
		}
	}
}
