using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal interface IEntityChunkAction<T1, T2>
	where T1 : struct
	where T2 : struct
{
	void Invoke(Entity entity, ref T1 component1, in T2 component2);
}
