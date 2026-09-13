namespace Bezoro.ECS.Internal;

internal interface IChunkAction<T1> where T1 : struct
{
	void Invoke(ref T1 component1);
}
