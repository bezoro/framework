using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class SystemState(ISystem system, Stage stage, int[] readIds, int[] writeIds, bool isExclusive)
{
	public bool IsExclusive { get; } = isExclusive;

	public SystemLoopPhase LoopPhase { get; } = system.LoopPhase;

	public int[] ReadIds { get; } = readIds;

	public int[] WriteIds { get; } = writeIds;

	public ISystem System { get; } = system;

	public Stage Stage { get; } = stage;

	public float Accumulator { get; set; }
}
