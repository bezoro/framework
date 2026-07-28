using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.ActivationSystem.Types;

namespace Bezoro.GameSystems.Tests.ActivationSystem;

[ReadsResource(typeof(ActivationConfig))]
internal sealed class ActivationConfigReaderSystem : ISystem
{
	public bool ObservedConfig { get; private set; }

	public void Update(in SystemContext context)
	{
		_ = context.World.ReadResource<ActivationConfig>();
		ObservedConfig = true;
	}
}
