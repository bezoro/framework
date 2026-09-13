using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal interface IEntityChunkAction<T1> where T1 : struct
{
	void Invoke(Entity entity, ref T1 component1);
}
