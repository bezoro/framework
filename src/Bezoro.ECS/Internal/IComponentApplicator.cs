using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal interface IComponentApplicator
{
	void Apply(World world, Entity entity);
}
