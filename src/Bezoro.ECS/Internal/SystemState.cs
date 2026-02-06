using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class SystemState
{
	public SystemState(ISystem system, Stage stage, int[] readIds, int[] writeIds, bool isExclusive)
	{
		System      = system;
		Stage       = stage;
		ReadIds     = readIds;
		WriteIds    = writeIds;
		IsExclusive = isExclusive;
	}

	public bool IsExclusive { get; }

	public int[] ReadIds { get; }

	public int[] WriteIds { get; }

	public ISystem System { get; }

	public Stage Stage { get; }

	public float Accumulator { get; set; }
}
