using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
/// Represents a world context that manages entities, components, resources, and systems.
/// </summary>
public interface IWorld
{
	bool IsAlive(Entity entity);

	Entity Spawn();

	Entity Spawn<T1>(in T1 component1)
		where T1 : struct, IComponent;

	Entity Spawn<T1, T2>(in T1 component1, in T2 component2)
		where T1 : struct, IComponent
		where T2 : struct, IComponent;

	Entity Spawn<T1, T2, T3>(in T1 component1, in T2 component2, in T3 component3)
		where T1 : struct, IComponent
		where T2 : struct, IComponent
		where T3 : struct, IComponent;

	Entity Spawn<T1, T2, T3, T4>(in T1 component1, in T2 component2, in T3 component3, in T4 component4)
		where T1 : struct, IComponent
		where T2 : struct, IComponent
		where T3 : struct, IComponent
		where T4 : struct, IComponent;

	void Despawn(Entity entity);

	bool Has<T>(Entity entity) where T : struct, IComponent;

	ref T Get<T>(Entity entity) where T : struct, IComponent;

	bool TryGet<T>(Entity entity, out T component) where T : struct, IComponent;

	void Set<T>(Entity entity, in T component) where T : struct, IComponent;

	void Add<T>(Entity entity) where T : struct, IComponent;

	void Add<T>(Entity entity, in T component) where T : struct, IComponent;

	void Remove<T>(Entity entity) where T : struct, IComponent;

	Query Query();

	Query Query<T1>()
		where T1 : struct, IComponent =>
		Query().All<T1>();

	Query Query<T1, T2>()
		where T1 : struct, IComponent
		where T2 : struct, IComponent =>
		Query().All<T1>().All<T2>();

	Query Query<T1, T2, T3>()
		where T1 : struct, IComponent
		where T2 : struct, IComponent
		where T3 : struct, IComponent =>
		Query().All<T1>().All<T2>().All<T3>();

	Query Query<T1, T2, T3, T4>()
		where T1 : struct, IComponent
		where T2 : struct, IComponent
		where T3 : struct, IComponent
		where T4 : struct, IComponent =>
		Query().All<T1>().All<T2>().All<T3>().All<T4>();

	Query Query(Archetype archetype);

	void SetResource<T>(T resource) where T : notnull;

	ref T GetResource<T>() where T : notnull;
}
