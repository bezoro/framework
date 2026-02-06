using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
/// Represents a world context that manages entities, components, resources, and systems.
/// </summary>
public interface IWorld
{
	bool IsAlive(Entity entity);

	Entity Spawn();

	void Despawn(Entity entity);

	bool Has<T>(Entity entity) where T : struct, IComponent;

	ref T Get<T>(Entity entity) where T : struct, IComponent;

	bool TryGet<T>(Entity entity, out T component) where T : struct, IComponent;

	void Set<T>(Entity entity, in T component) where T : struct, IComponent;

	void Add<T>(Entity entity, in T component) where T : struct, IComponent;

	void Remove<T>(Entity entity) where T : struct, IComponent;

	Query Query();

	Query Query(Archetype archetype);

	void SetResource<T>(T resource) where T : notnull;

	ref T GetResource<T>() where T : notnull;

	// Compatibility aliases for existing callers.
	bool HasComponent<T>(Entity entity) where T : struct, IComponent => Has<T>(entity);

	T GetComponent<T>(Entity entity) where T : struct, IComponent => Get<T>(entity);

	bool TryGetComponent<T>(Entity entity, out T component) where T : struct, IComponent => TryGet(entity, out component);

	void SetComponent<T>(Entity entity, in T component) where T : struct, IComponent => Set(entity, in component);

	void AddComponent<T>(Entity entity, in T component) where T : struct, IComponent => Add(entity, in component);

	void RemoveComponent<T>(Entity entity) where T : struct, IComponent => Remove<T>(entity);
}
