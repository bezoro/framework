using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Internal;

internal sealed class SystemState
{
	public SystemState(ISystem system, int[] readIds, int[] writeIds)
	{
		System   = system;
		ReadIds  = readIds;
		WriteIds = writeIds;
	}

	public ISystem System { get; }

	public int[] ReadIds { get; }

	public int[] WriteIds { get; }

	public float Accumulator { get; set; }
}
