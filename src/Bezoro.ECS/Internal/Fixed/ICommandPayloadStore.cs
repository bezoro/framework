using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal.Fixed;

internal interface ICommandPayloadStore
{
	void Apply(World world, Entity entity, int payloadIndex);

	void ApplyBatch(
		World world,
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

