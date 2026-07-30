using System.Threading;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Tests.Services;

internal readonly struct ParallelEntityProbeJob(
	Entity[] receivedEntities,
	int[]    checksums,
	int[]    visitCounts)
	: IForEachEntity<ParallelEntityProbeComponent>,
	  IForEachEntity<ParallelEntityProbeComponent, ParallelEntityProbeInput2, ParallelEntityProbeInput3>,
	  IForEachEntity<ParallelEntityProbeComponent, ParallelEntityProbeInput2, ParallelEntityProbeInput3,
		  ParallelEntityProbeInput4>
{
	public void Execute(Entity entity, ref ParallelEntityProbeComponent component1) =>
		Record(entity, ref component1, 0);

	public void Execute(
		Entity                           entity,
		ref ParallelEntityProbeComponent component1,
		in ParallelEntityProbeInput2     component2,
		in ParallelEntityProbeInput3     component3) =>
		Record(entity, ref component1, component2.Value + component3.Value);

	public void Execute(
		Entity                           entity,
		ref ParallelEntityProbeComponent component1,
		in ParallelEntityProbeInput2     component2,
		in ParallelEntityProbeInput3     component3,
		in ParallelEntityProbeInput4     component4) =>
		Record(entity, ref component1, component2.Value + component3.Value + component4.Value);

	private void Record(Entity entity, ref ParallelEntityProbeComponent component, int checksum)
	{
		int index               = component.Index;
		receivedEntities[index] = entity;
		checksums[index]         = checksum;
		Interlocked.Increment(ref visitCounts[index]);
		component.ExecutionCount++;
	}
}
