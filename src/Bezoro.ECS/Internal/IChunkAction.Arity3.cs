namespace Bezoro.ECS.Internal;

internal interface IChunkAction<T1, T2, T3>
	where T1 : struct
	where T2 : struct
	where T3 : struct
{
	void Invoke(ref T1 component1, in T2 component2, in T3 component3);
}
