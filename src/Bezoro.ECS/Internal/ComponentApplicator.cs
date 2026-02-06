using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class ComponentApplicator<T> : IComponentApplicator where T : struct, IComponent
{
	private readonly bool _addOnly;
	private readonly int  _typeId;
	private readonly T    _component;

	public ComponentApplicator(int typeId, in T component, bool addOnly)
	{
		_addOnly   = addOnly;
		_typeId    = typeId;
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
