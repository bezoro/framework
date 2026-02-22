using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class SystemState(
	ISystem system,
	Stage   stage,
	int[]   readIds,
	int[]   writeIds,
	bool    isExclusive,
	Type[]  beforeSystemTypes,
	Type[]  afterSystemTypes,
	Type[]  systemSetTypes,
	ISystemRunCondition[] runConditions)
{
	public Type[] AfterSystemTypes { get; } = afterSystemTypes;

	public Type[] BeforeSystemTypes { get; } = beforeSystemTypes;

	public bool IsExclusive { get; } = isExclusive;

	public int[] ReadIds { get; } = readIds;

	public int[] WriteIds { get; } = writeIds;

	public ISystem System { get; } = system;

	public Stage Stage { get; } = stage;

	public SystemLoopPhase LoopPhase { get; } = system.LoopPhase;

	public float Accumulator { get; set; }

	public ISystemRunCondition[] RunConditions { get; } = runConditions;

	public Type[] SystemSetTypes { get; } = systemSetTypes;
}
