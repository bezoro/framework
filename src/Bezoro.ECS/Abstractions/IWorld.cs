using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Represents a world context that manages entities, components, resources, and systems.
/// </summary>
public interface IWorld
{
	bool Has<T>(Entity  entity) where T : struct;
	bool IsAlive(Entity entity);

	bool TryGet<T>(Entity entity, out T component) where T : struct;

	Entity Spawn();

	Entity Spawn<T1>(in T1 component1)
		where T1 : struct;

	Entity Spawn<T1, T2>(in T1 component1, in T2 component2)
		where T1 : struct
		where T2 : struct;

	Entity Spawn<T1, T2, T3>(in T1 component1, in T2 component2, in T3 component3)
		where T1 : struct
		where T2 : struct
		where T3 : struct;

	Entity Spawn<T1, T2, T3, T4>(in T1 component1, in T2 component2, in T3 component3, in T4 component4)
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct;

	ref T Get<T>(Entity entity) where T : struct;

	ref T GetResource<T>() where T : notnull;

	void Add<T>(Entity entity) where T : struct;

	void Add<T>(Entity entity, in T component) where T : struct;

	void Despawn(Entity entity);

	void Remove<T>(Entity entity) where T : struct;

	void Set<T>(Entity entity, in T component) where T : struct;

	void SetResource<T>(T resource) where T : notnull;
}
