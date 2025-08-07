using System.Collections.Generic;

namespace Bezoro.Core.ECS;

public class EntityManager
{
	private readonly Queue<int> availableIds = new();
	private          int        nextId;

	public Entity CreateEntity()
	{
		int id = availableIds.Count > 0 ? availableIds.Dequeue() : nextId++;
		return new(id);
	}

	public void DestroyEntity(Entity entity) =>
		availableIds.Enqueue(entity.Id);
}
