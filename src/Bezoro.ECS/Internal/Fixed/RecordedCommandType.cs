namespace Bezoro.ECS.Internal.Fixed;

internal enum RecordedCommandType : byte
{
	CreateEntity              = 0,
	DestroyEntity             = 1,
	SetComponent              = 2,
	RemoveComponent           = 3,
	CreateEntityWithComponent = 4
}
