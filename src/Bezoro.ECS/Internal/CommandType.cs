namespace Bezoro.ECS.Internal;

internal enum CommandType
{
	CreateEntity,
	CreateEntityWithComponent,
	DestroyEntity,
	AddComponent,
	RemoveComponent,
	SetComponent
}
