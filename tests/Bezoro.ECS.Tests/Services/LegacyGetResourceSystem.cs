using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Tests.Services;

internal sealed class LegacyGetResourceSystem : ISystem
{
	public void Update(in SystemContext context)
	{
#pragma warning disable CS0618
		ref var resource = ref context.World.GetResource<ErgonomicSchedulerResource>();
#pragma warning restore CS0618
	}
}
