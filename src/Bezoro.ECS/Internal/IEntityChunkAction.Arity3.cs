using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal interface IEntityChunkAction<T1, T2, T3>
	where T1 : struct
	where T2 : struct
	where T3 : struct
{
	void Invoke(Entity entity, ref T1 component1, in T2 component2, in T3 component3);
}
