using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class ComponentApplicator<T>(int typeId, in T component, bool addOnly) : IComponentApplicator
	where T : struct, IComponent
{
	private readonly T _component = component;

	public void Apply(World world, Entity entity)
	{
		if (addOnly)
		{
			world.ApplyAddComponentTyped(entity, typeId, in _component);
			return;
		}

		world.ApplySetComponentTyped(entity, typeId, in _component);
	}
}
