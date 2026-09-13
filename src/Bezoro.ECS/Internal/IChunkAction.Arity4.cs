namespace Bezoro.ECS.Internal;

internal interface IChunkAction<T1, T2, T3, T4>
	where T1 : struct
	where T2 : struct
	where T3 : struct
	where T4 : struct
{
	void Invoke(ref T1 component1, in T2 component2, in T3 component3, in T4 component4);
}
