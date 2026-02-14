using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal.V2;

internal interface ICommandPayloadStore
{
	void Apply(WorldV2 world, Entity entity, int payloadIndex);

	void ApplyBatch(
		WorldV2 world,
		int[]   entityIds,
		int     entityOffset,
		int     count,
		int[]   payloadIndices,
		int     payloadOffset,
		int     componentTypeId,
		int     sourceArchetypeId,
		int     targetArchetypeId
	);

	void Clear();

	void Dispose();
}
