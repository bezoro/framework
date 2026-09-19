using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Tests.Services;

internal readonly struct ParallelEntityProbeQuery : ICompiledQuerySpec
{
	public void Build(ref QueryBuilder builder)
	{
		builder.All<ParallelEntityProbeComponent>();
		builder.All<ParallelEntityProbeInput2>();
		builder.All<ParallelEntityProbeInput3>();
		builder.All<ParallelEntityProbeInput4>();
	}
}
