using Bezoro.ECS.Types;

namespace Bezoro.GameSystems.ActivationSystem.Services;

internal readonly struct PendingActivationCandidate
{
	public PendingActivationCandidate(Entity entryEntity, int priority, int handleId)
	{
		EntryEntity = entryEntity;
		Priority = priority;
		HandleId = handleId;
	}

	public Entity EntryEntity { get; }

	public int Priority { get; }

	public int HandleId { get; }
}
