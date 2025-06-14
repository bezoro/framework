namespace Bezoro.Core.ECS
{
	public class World
	{
		private readonly ComponentManager componentManager = new();
		private readonly EntityManager    entityManager    = new();
		private readonly SystemManager    systemManager    = new();

		public bool HasComponent<T>(Entity entity) where T : struct, IComponent =>
			componentManager.HasComponent<T>(entity);

		public Entity CreateEntity() =>
			entityManager.CreateEntity();

		public T GetComponent<T>(Entity entity) where T : struct, IComponent =>
			componentManager.GetComponent<T>(entity);

		public void AddComponent<T>(Entity entity, T component) where T : struct, IComponent =>
			componentManager.AddComponent(entity, component);

		public void DestroyEntity(Entity entity)
		{
			componentManager.RemoveAllComponents(entity);
			entityManager.DestroyEntity(entity);
		}

		public void RegisterSystem(ISystem system) =>
			systemManager.RegisterSystem(system);

		public void RemoveComponent<T>(Entity entity) where T : struct, IComponent =>
			componentManager.RemoveComponent<T>(entity);

		public void Update() =>
			systemManager.UpdateAll();
	}
}
