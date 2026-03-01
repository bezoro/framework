using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class WorldRelationIndex
{
	private readonly Dictionary<(Type relationType, int targetId, int targetVersion), int> _relationTypeIdByKey = [];
	private readonly Dictionary<(int targetId, int targetVersion), int[]> _relationTypeIdsByTarget = [];
	private readonly Dictionary<Type, int[]> _relationTypeIdsByRelationType = [];
	private readonly Dictionary<int, RelationshipInfo> _relationInfoByTypeId = [];

	public void Clear()
	{
		_relationTypeIdByKey.Clear();
		_relationTypeIdsByTarget.Clear();
		_relationTypeIdsByRelationType.Clear();
		_relationInfoByTypeId.Clear();
	}

	public bool TryGetRelationInfo(int relationTypeId, out RelationshipInfo info) =>
		_relationInfoByTypeId.TryGetValue(relationTypeId, out info);

	public int GetOrCreateRelationTypeId(Type relationType, Entity target, Func<int> relationTypeIdFactory)
	{
		if (relationType is null) throw new ArgumentNullException(nameof(relationType));
		if (target == Entity.None || target == Entity.Wildcard)
			throw new ArgumentException("Relation target must be a concrete entity.", nameof(target));

		var key = (relationType, target.Id, target.Version);
		if (_relationTypeIdByKey.TryGetValue(key, out int existing))
			return existing;

		int typeId = relationTypeIdFactory();
		_relationTypeIdByKey[key]     = typeId;
		_relationInfoByTypeId[typeId] = new(relationType, target);
		AppendRelationTypeId(_relationTypeIdsByRelationType, relationType, typeId);
		AppendRelationTypeId(_relationTypeIdsByTarget, (target.Id, target.Version), typeId);
		return typeId;
	}

	public int[] GetRelationTypeIds(Type relationType)
	{
		if (relationType is null) throw new ArgumentNullException(nameof(relationType));

		return _relationTypeIdsByRelationType.TryGetValue(relationType, out int[]? ids) ? ids : [];
	}

	public bool TryGetRelationTypeId(Type relationType, Entity target, out int relationTypeId)
	{
		if (relationType is null) throw new ArgumentNullException(nameof(relationType));

		return _relationTypeIdByKey.TryGetValue((relationType, target.Id, target.Version), out relationTypeId);
	}

	public void ReleaseRelationsForTarget(Entity target, Action<int> removeRelationTypeFromAllSources)
	{
		if (!_relationTypeIdsByTarget.TryGetValue((target.Id, target.Version), out int[]? relationTypeIds) ||
			relationTypeIds.Length == 0)
			return;

		_relationTypeIdsByTarget.Remove((target.Id, target.Version));

		for (var i = 0; i < relationTypeIds.Length; i++)
		{
			int relationTypeId = relationTypeIds[i];
			removeRelationTypeFromAllSources(relationTypeId);
			if (!_relationInfoByTypeId.TryGetValue(relationTypeId, out var info))
				continue;

			_relationInfoByTypeId.Remove(relationTypeId);
			_relationTypeIdByKey.Remove((info.RelationType, info.Target.Id, info.Target.Version));
			RemoveRelationTypeId(_relationTypeIdsByRelationType, info.RelationType, relationTypeId);
		}
	}

	private static void AppendRelationTypeId<TKey>(Dictionary<TKey, int[]> map, TKey key, int relationTypeId)
		where TKey : notnull
	{
		if (!map.TryGetValue(key, out int[]? existing))
		{
			map[key] = [relationTypeId];
			return;
		}

		var updated = new int[existing.Length + 1];
		Array.Copy(existing, updated, existing.Length);
		updated[^1] = relationTypeId;
		map[key] = updated;
	}

	private static void RemoveRelationTypeId<TKey>(Dictionary<TKey, int[]> map, TKey key, int relationTypeId)
		where TKey : notnull
	{
		if (!map.TryGetValue(key, out int[]? existing))
			return;

		int index = Array.IndexOf(existing, relationTypeId);
		if (index < 0)
			return;

		if (existing.Length == 1)
		{
			map.Remove(key);
			return;
		}

		var updated = new int[existing.Length - 1];
		if (index > 0)
			Array.Copy(existing, 0, updated, 0, index);

		if (index < existing.Length - 1)
			Array.Copy(existing, index + 1, updated, index, existing.Length - index - 1);

		map[key] = updated;
	}
}
