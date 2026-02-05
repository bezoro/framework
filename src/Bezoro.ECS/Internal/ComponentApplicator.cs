using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class ComponentApplicator<T> : IComponentApplicator where T : struct, IComponent
{
	private readonly bool _addOnly;
	private readonly T   _component;
	private readonly int _typeId;

	public ComponentApplicator(int typeId, in T component, bool addOnly)
	{
		_addOnly  = addOnly;
		_typeId   = typeId;
		_component = component;
	}

	public void Apply(World world, Entity entity)
	{
		if (_addOnly)
		{
			world.ApplyAddComponentTyped(entity, _typeId, in _component);
			return;
		}

		world.ApplySetComponentTyped(entity, _typeId, in _component);
	}
}
