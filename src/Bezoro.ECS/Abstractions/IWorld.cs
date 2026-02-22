using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Represents a world context that manages entities, components, resources, and systems.
/// </summary>
public interface IWorld
{
	/// <summary>Returns <c>true</c> if <paramref name="entity" /> has component <typeparamref name="T" />.</summary>
	bool Has<T>(Entity entity) where T : struct;

	/// <summary>Returns <c>true</c> if <paramref name="entity" /> is alive in this world.</summary>
	bool IsAlive(Entity entity);

	/// <summary>
	///     Attempts to retrieve a copy of component <typeparamref name="T" /> on <paramref name="entity" />.
	/// </summary>
	/// <returns><c>true</c> if the component exists; <c>false</c> otherwise.</returns>
	bool TryGet<T>(Entity entity, out T component) where T : struct;

	/// <summary>Creates a new entity with no components and returns its handle.</summary>
	Entity Spawn();

	/// <summary>Creates a new entity with one initial component and returns its handle.</summary>
	Entity Spawn<T1>(in T1 component1)
		where T1 : struct;

	/// <summary>Creates a new entity with two initial components and returns its handle.</summary>
	Entity Spawn<T1, T2>(in T1 component1, in T2 component2)
		where T1 : struct
		where T2 : struct;

	/// <summary>Creates a new entity with three initial components and returns its handle.</summary>
	Entity Spawn<T1, T2, T3>(in T1 component1, in T2 component2, in T3 component3)
		where T1 : struct
		where T2 : struct
		where T3 : struct;

	/// <summary>Creates a new entity with four initial components and returns its handle.</summary>
	Entity Spawn<T1, T2, T3, T4>(in T1 component1, in T2 component2, in T3 component3, in T4 component4)
		where T1 : struct
		where T2 : struct
		where T3 : struct
		where T4 : struct;

	/// <summary>Returns a mutable reference to component <typeparamref name="T" /> on <paramref name="entity" />.</summary>
	ref T Get<T>(Entity entity) where T : struct;

	/// <summary>Returns a reference to the registered resource of type <typeparamref name="T" />.</summary>
	ref T GetResource<T>() where T : notnull;

	/// <summary>Adds a default-valued component <typeparamref name="T" /> to <paramref name="entity" />.</summary>
	void Add<T>(Entity entity) where T : struct;

	/// <summary>Adds component <typeparamref name="T" /> with the given value to <paramref name="entity" />.</summary>
	void Add<T>(Entity entity, in T component) where T : struct;

	/// <summary>
	///     Adds a relation of type <typeparamref name="TRelation" /> from <paramref name="source" /> to <paramref name="target" />.
	/// </summary>
	void AddRelation<TRelation>(Entity source, Entity target)
		where TRelation : struct;

	/// <summary>Destroys <paramref name="entity" /> and invalidates its handle.</summary>
	void Despawn(Entity entity);

	/// <summary>
	///     Returns <c>true</c> when <paramref name="source" /> has relation <typeparamref name="TRelation" /> to <paramref name="target" />.
	/// </summary>
	bool HasRelation<TRelation>(Entity source, Entity target)
		where TRelation : struct;

	/// <summary>Removes component <typeparamref name="T" /> from <paramref name="entity" />.</summary>
	void Remove<T>(Entity entity) where T : struct;

	/// <summary>
	///     Removes relation <typeparamref name="TRelation" /> from <paramref name="source" /> to <paramref name="target" />.
	/// </summary>
	/// <returns><c>true</c> when a relation existed and was removed; otherwise <c>false</c>.</returns>
	bool RemoveRelation<TRelation>(Entity source, Entity target)
		where TRelation : struct;

	/// <summary>Sets component <typeparamref name="T" /> on <paramref name="entity" /> to <paramref name="component" />.</summary>
	void Set<T>(Entity entity, in T component) where T : struct;

	/// <summary>Registers or replaces the resource of type <typeparamref name="T" />.</summary>
	void SetResource<T>(T resource) where T : notnull;
}
