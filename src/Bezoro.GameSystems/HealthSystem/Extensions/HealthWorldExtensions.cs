using System;
using Bezoro.Core.Types;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.HealthSystem.Types;

namespace Bezoro.GameSystems.HealthSystem.Extensions;

/// <summary>
///     Helper methods for queueing health mutations.
/// </summary>
public static class HealthWorldExtensions
{
	/// <summary>
	///     Queues a damage request for the target entity.
	/// </summary>
	/// <param name="world">World containing the target entity.</param>
	/// <param name="targetEntity">Entity to damage.</param>
	/// <param name="amount">Damage amount.</param>
	/// <returns><c>true</c> when queued; otherwise <c>false</c>.</returns>
	public static bool QueueHealthDamage(this IWorld world, Entity targetEntity, uint amount) =>
		Queue(world, targetEntity, HealthMutationKind.Damage, amount, MaxValueUpdateMode.ClampCurrent);

	/// <summary>
	///     Queues a heal request for the target entity.
	/// </summary>
	/// <param name="world">World containing the target entity.</param>
	/// <param name="targetEntity">Entity to heal.</param>
	/// <param name="amount">Heal amount applied to base health only.</param>
	/// <returns><c>true</c> when queued; otherwise <c>false</c>.</returns>
	public static bool QueueHealthHeal(this IWorld world, Entity targetEntity, uint amount) =>
		Queue(world, targetEntity, HealthMutationKind.Heal, amount, MaxValueUpdateMode.ClampCurrent);

	/// <summary>
	///     Queues a direct damage request that ignores excess health.
	/// </summary>
	/// <param name="world">World containing the target entity.</param>
	/// <param name="targetEntity">Entity to damage.</param>
	/// <param name="amount">Damage amount applied to base health only.</param>
	/// <returns><c>true</c> when queued; otherwise <c>false</c>.</returns>
	public static bool QueueHealthDirectDamage(this IWorld world, Entity targetEntity, uint amount) =>
		Queue(world, targetEntity, HealthMutationKind.DirectDamage, amount, MaxValueUpdateMode.ClampCurrent);

	/// <summary>
	///     Queues an increase-health request that can overflow into excess.
	/// </summary>
	/// <param name="world">World containing the target entity.</param>
	/// <param name="targetEntity">Entity to heal.</param>
	/// <param name="amount">Heal amount that can overflow from base health into excess.</param>
	/// <returns><c>true</c> when queued; otherwise <c>false</c>.</returns>
	public static bool QueueHealthIncreaseHealth(this IWorld world, Entity targetEntity, uint amount) =>
		Queue(world, targetEntity, HealthMutationKind.IncreaseHealth, amount, MaxValueUpdateMode.ClampCurrent);

	/// <summary>
	///     Queues a max-health change request for the target entity.
	/// </summary>
	/// <param name="world">World containing the target entity.</param>
	/// <param name="targetEntity">Entity whose max health should be changed.</param>
	/// <param name="maxHealth">New max health value.</param>
	/// <param name="mode">How current health should be updated.</param>
	/// <returns><c>true</c> when queued; otherwise <c>false</c>.</returns>
	public static bool QueueSetHealthMax(
		this IWorld        world,
		Entity             targetEntity,
		uint               maxHealth,
		MaxValueUpdateMode mode = MaxValueUpdateMode.ClampCurrent) =>
		Queue(world, targetEntity, HealthMutationKind.SetMax, maxHealth, mode);

	private static bool Queue(
		IWorld             world,
		Entity             targetEntity,
		HealthMutationKind kind,
		uint               value,
		MaxValueUpdateMode maxUpdateMode)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		if (!world.IsAlive(targetEntity))
			return false;

		if (!world.Has<Health>(targetEntity))
			return false;

		world.Spawn(new HealthMutationRequest(targetEntity, kind, value, maxUpdateMode));
		return true;
	}
}
