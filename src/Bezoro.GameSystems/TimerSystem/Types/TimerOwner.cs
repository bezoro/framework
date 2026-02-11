using Bezoro.ECS.Types;

namespace Bezoro.GameSystems.TimerSystem.Types;

/// <summary>
///     Associates a timer entity with an owning gameplay entity.
/// </summary>
/// <param name="OwnerEntity">Owner entity for this timer.</param>
public readonly record struct TimerOwner(Entity OwnerEntity);
