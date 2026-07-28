using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Tests.Services;

internal sealed class LegacyGetComponentSystem(Entity entity) : ISystem
{
	public void Update(in SystemContext context)
	{
#pragma warning disable CS0618
		ref var position = ref context.World.Get<ErgonomicJobPosition>(entity);
#pragma warning restore CS0618
		position.Value++;
	}
}
