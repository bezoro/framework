using System.Collections.Generic;
using Xunit;

namespace Bezoro.ECS.Tests;

public class SystemManagerTests
{
	[Fact]
	public void UpdateAll_Should_Call_Update_On_Registered_Systems()
	{
		// Arrange
		var systemManager = new SystemManager();
		var testSystem    = new TestSystem();
		systemManager.RegisterSystem(testSystem);

		// Act
		systemManager.UpdateAll();

		// Assert
		Assert.True(testSystem.WasUpdated);
	}

	[Fact]
	public void UpdateAll_Should_Execute_Systems_In_Registration_Order()
	{
		// Arrange
		var systemManager  = new SystemManager();
		var executionOrder = new List<string>();
		var system1        = new OrderTestSystem("System1", executionOrder);
		var system2        = new OrderTestSystem("System2", executionOrder);

		systemManager.RegisterSystem(system1);
		systemManager.RegisterSystem(system2);

		// Act
		systemManager.UpdateAll();

		// Assert
		Assert.Equal(
			new() { "System1", "System2" },
			executionOrder);
	}

	private class OrderTestSystem : ISystem
	{
		private readonly List<string> _executionOrder;
		private readonly string       _name;

		public OrderTestSystem(string name, List<string> executionOrder)
		{
			_name           = name;
			_executionOrder = executionOrder;
		}

		#region Interface Implementations

		public void Update() => _executionOrder.Add(_name);

		#endregion
	}

	private class TestSystem : ISystem
	{
		public bool WasUpdated { get; private set; }

		#region Interface Implementations

		public void Update() => WasUpdated = true;

		#endregion
	}
}
