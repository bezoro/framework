using System;
using System.Collections.Generic;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.ActivationSystem.Types;

namespace Bezoro.GameSystems.ActivationSystem.Services;

/// <summary>
///     ECS system that applies activation cancellation, ordering, and per-tick activation budget.
/// </summary>
[Writes<ActivationEntry>]
[Reads<ActivationCancellationRequest>]
public sealed class ActivationProcessingSystem : ISystem
{
	/// <summary>
	///     Raised when all pending entries have been activated.
	/// </summary>
	public event Action<ActivationCompletedEvent>? Completed;

	public Stage Stage => Stage.Tick;

	public SystemLoopPhase LoopPhase => SystemLoopPhase.Tick;

	/// <inheritdoc />
	public void OnCreate(World world)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		EnsureResources(world);
	}

	/// <inheritdoc />
	public void Update(IWorld world, in SystemContext context)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		EnsureResources(world);

		var entriesByHandle = BuildEntryIndex(world);
		ApplyCancellations(world, context.Commands, entriesByHandle);

		var pendingEntries = CollectPendingEntries(world, out var activatedCount);
		ref var runtime = ref world.GetResource<ActivationRuntimeState>();
		if (pendingEntries.Count > 0)
			runtime.CompletionPublished = false;

		if (pendingEntries.Count > 1)
			pendingEntries.Sort(static (left, right) =>
				{
					var priorityComparison = right.Priority.CompareTo(left.Priority);
					return priorityComparison != 0
							   ? priorityComparison
							   : left.HandleId.CompareTo(right.HandleId);
				}
			);

		ref var config = ref world.GetResource<ActivationConfig>();
		ref var dispatchQueue = ref world.GetResource<ActivationDispatchQueueResource>();

		var maxActivationsPerTick = config.MaxActivationsPerTick <= 0
										? int.MaxValue
										: config.MaxActivationsPerTick;
		var targetActivationCount = Math.Min(maxActivationsPerTick, pendingEntries.Count);

		var activatedThisTick = 0;
		for (var i = 0; i < targetActivationCount; i++)
		{
			var candidate = pendingEntries[i];
			if (!world.TryGet(candidate.EntryEntity, out ActivationEntry entry))
				continue;

			if (entry.State != ActivationState.Pending)
				continue;

			entry.State = ActivationState.Activated;
			world.Set(candidate.EntryEntity, in entry);
			dispatchQueue.Enqueue(entry.Callback);
			activatedThisTick++;
		}

		activatedCount += activatedThisTick;
		var pendingCount = pendingEntries.Count - activatedThisTick;
		runtime.SetCounts(activatedCount, pendingCount);

		PublishCompletionIfNeeded(world, runtime);
	}

	private void PublishCompletionIfNeeded(IWorld world, ActivationRuntimeState runtime)
	{
		if (runtime.PendingCount == 0 && runtime.ActivatedCount > 0)
		{
			if (runtime.CompletionPublished)
				return;

			var eventData = new ActivationCompletedEvent(runtime.ActivatedCount);
			ref var events = ref world.GetResource<ActivationEventsResource>();
			events.Enqueue(in eventData);

			try
			{
				Completed?.Invoke(eventData);
			}
			catch
			{
				// Event handler exceptions should not break simulation.
			}

			runtime.CompletionPublished = true;
			return;
		}

		runtime.CompletionPublished = false;
	}

	private static List<PendingActivationCandidate> CollectPendingEntries(
		IWorld world,
		out int activatedCount)
	{
		var pendingEntries = new List<PendingActivationCandidate>();
		activatedCount = 0;

		var enumerator = world.Query().All<ActivationEntry>().GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				var chunk = enumerator.Current;
				var entities = chunk.Entities;
				var entries = chunk.ReadOnlyComponents<ActivationEntry>();
				for (var i = 0; i < chunk.Count; i++)
				{
					ref readonly var entry = ref entries[i];
					if (entry.State == ActivationState.Activated)
					{
						activatedCount++;
						continue;
					}

					if (entry.State != ActivationState.Pending)
						continue;

					pendingEntries.Add(new(entities[i], entry.Priority, entry.Handle.Id));
				}
			}
		}
		finally
		{
			enumerator.Dispose();
		}

		return pendingEntries;
	}

	private static Dictionary<int, Entity> BuildEntryIndex(IWorld world)
	{
		var entriesByHandle = new Dictionary<int, Entity>();
		var enumerator = world.Query().All<ActivationEntry>().GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				var chunk = enumerator.Current;
				var entities = chunk.Entities;
				var entries = chunk.ReadOnlyComponents<ActivationEntry>();
				for (var i = 0; i < chunk.Count; i++)
				{
					ref readonly var entry = ref entries[i];
					if (!entry.Handle.IsValid)
						continue;

					entriesByHandle[entry.Handle.Id] = entities[i];
				}
			}
		}
		finally
		{
			enumerator.Dispose();
		}

		return entriesByHandle;
	}

	private static void ApplyCancellations(
		IWorld                  world,
		CommandBuffer           commands,
		IReadOnlyDictionary<int, Entity> entriesByHandle)
	{
		var enumerator = world.Query().All<ActivationCancellationRequest>().GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				var chunk = enumerator.Current;
				var entities = chunk.Entities;
				var requests = chunk.ReadOnlyComponents<ActivationCancellationRequest>();

				for (var i = 0; i < chunk.Count; i++)
				{
					var requestEntity = entities[i];
					ref readonly var request = ref requests[i];

					if (entriesByHandle.TryGetValue(request.Handle.Id, out var entryEntity) &&
						world.TryGet(entryEntity, out ActivationEntry entry) &&
						entry.State == ActivationState.Pending)
					{
						entry.State = ActivationState.Cancelled;
						world.Set(entryEntity, in entry);
					}

					commands.DestroyEntity(requestEntity);
				}
			}
		}
		finally
		{
			enumerator.Dispose();
		}
	}

	private static void EnsureResources(IWorld world)
	{
		try
		{
			_ = world.GetResource<ActivationConfig>();
		}
		catch (KeyNotFoundException)
		{
			world.SetResource(new ActivationConfig());
		}

		try
		{
			_ = world.GetResource<ActivationRuntimeState>();
		}
		catch (KeyNotFoundException)
		{
			world.SetResource(new ActivationRuntimeState());
		}

		try
		{
			_ = world.GetResource<ActivationEventsResource>();
		}
		catch (KeyNotFoundException)
		{
			world.SetResource(new ActivationEventsResource());
		}

		try
		{
			_ = world.GetResource<ActivationDispatchQueueResource>();
		}
		catch (KeyNotFoundException)
		{
			world.SetResource(new ActivationDispatchQueueResource());
		}
	}

}
