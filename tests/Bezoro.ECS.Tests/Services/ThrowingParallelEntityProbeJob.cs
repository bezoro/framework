using System;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Tests.Services;

internal readonly struct ThrowingParallelEntityProbeJob(Exception exception)
	: IForEachEntity<ParallelEntityProbeComponent>
{
	public void Execute(Entity entity, ref ParallelEntityProbeComponent component1) => throw exception;
}
