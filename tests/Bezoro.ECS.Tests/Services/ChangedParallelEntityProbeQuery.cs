using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Tests.Services;

internal readonly struct ChangedParallelEntityProbeQuery : ICompiledQuerySpec
{
	public void Build(ref QueryBuilder builder) => builder.Changed<ParallelEntityProbeComponent>();
}
