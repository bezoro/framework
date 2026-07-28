using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Tests.Services;

internal sealed class LegacyTryGetManagedSystem(Entity entity) : ISystem
{
	public void Update(in SystemContext context)
	{
#pragma warning disable CS0618
		_ = context.World.TryGetManaged(entity, out ErgonomicReadOnlyNote _);
#pragma warning restore CS0618
	}
}
