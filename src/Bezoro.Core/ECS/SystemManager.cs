using System.Collections.Generic;

namespace Bezoro.Core.ECS
{
	public class SystemManager
	{
		private readonly List<ISystem> systems = new();

		public void RegisterSystem(ISystem system) =>
			systems.Add(system);

		public void UpdateAll()
		{
			foreach (var system in systems)
			{
				system.Update();
			}
		}
	}
}
