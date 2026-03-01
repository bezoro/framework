using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal static class StageOrder
{
	public static ReadOnlySpan<Stage> Stages => [Stage.Input, Stage.PreTick, Stage.Tick, Stage.PostTick, Stage.Render];

	public static ReadOnlySpan<SystemLoopPhase> LoopPhases => [SystemLoopPhase.Tick, SystemLoopPhase.FixedTick, SystemLoopPhase.LateTick];
}
