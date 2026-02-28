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
[ReadsResource<ActivationConfig>]
[WritesResource<ActivationRuntimeState>]
[WritesResource<ActivationEventsResource>]
[WritesResource<ActivationDispatchQueueResource>]
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
	public void Update(in SystemContext context)
	{
		var world = context.World;
		if (world is null) throw new ArgumentNullException(nameof(world));
		EnsureResources(world);

		var entriesByHandle = BuildEntryIndex(world);
		ApplyCancellations(world, context.Commands, entriesByHandle);

		var pendingEntries = CollectPendingEntries(world, out var activatedCount);
		ref var runtime = ref world.WriteResource<ActivationRuntimeState>();
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

		ref readonly var config = ref world.ReadResource<ActivationConfig>();
		ref var dispatchQueue = ref world.WriteResource<ActivationDispatchQueueResource>();

		var maxActivationsPerTick = config.MaxActivationsPerTick <= 0
										? int.MaxValue
										: config.MaxActivationsPerTick;
		var targetActivationCount = Math.Min(maxActivationsPerTick, pendingEntries.Count);

		var activatedThisTick = 0;
		for (var i = 0; i < targetActivationCount; i++)
		{
			var candidate = pendingEntries[i];
			if (!world.TryWrite<ActivationEntry>(candidate.EntryEntity, out var entryRef))
				continue;

			ref var entry = ref entryRef.Value;
			if (entry.State != ActivationState.Pending)
				continue;

			entry.State = ActivationState.Activated;
			dispatchQueue.Enqueue(entry.Callback);
			activatedThisTick++;
		}

		activatedCount += activatedThisTick;
		var pendingCount = pendingEntries.Count - activatedThisTick;
		runtime.SetCounts(activatedCount, pendingCount);

		PublishCompletionIfNeeded(world, runtime);
	}

	private void PublishCompletionIfNeeded(World world, ActivationRuntimeState runtime)
	{
		if (runtime.PendingCount == 0 && runtime.ActivatedCount > 0)
		{
			if (runtime.CompletionPublished)
				return;

			var eventData = new ActivationCompletedEvent(runtime.ActivatedCount);
			ref var events = ref world.GetOrCreateResource<ActivationEventsResource>();
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

	private static List<PendingActivationCandidate> CollectPendingEntries(World world, out int activatedCount)
	{
		var pendingEntries = new List<PendingActivationCandidate>();
		var activatedCountLocal = 0;

		world.Query<ActivationEntryQuery>().ForEachRead<ActivationEntry>(
			(Entity entryEntity, in ActivationEntry entry) =>
			{
				if (entry.State == ActivationState.Activated)
				{
					activatedCountLocal++;
					return;
				}

				if (entry.State != ActivationState.Pending)
					return;

				pendingEntries.Add(new(entryEntity, entry.Priority, entry.Handle.Id));
			}
		);

		activatedCount = activatedCountLocal;
		return pendingEntries;
	}

	private static Dictionary<int, Entity> BuildEntryIndex(World world)
	{
		var entriesByHandle = new Dictionary<int, Entity>();
		world.Query<ActivationEntryQuery>().ForEachRead<ActivationEntry>(
			(Entity entryEntity, in ActivationEntry entry) =>
			{
				if (!entry.Handle.IsValid)
					return;

				entriesByHandle[entry.Handle.Id] = entryEntity;
			}
		);

		return entriesByHandle;
	}

	private static void ApplyCancellations(
		World                         world,
		CommandBuffer                 commands,
		IReadOnlyDictionary<int, Entity> entriesByHandle)
	{
		world.Query<ActivationCancellationQuery>().ForEachRead<ActivationCancellationRequest>(
			(Entity requestEntity, in ActivationCancellationRequest request) =>
			{
				if (entriesByHandle.TryGetValue(request.Handle.Id, out var entryEntity) &&
					world.TryWrite<ActivationEntry>(entryEntity, out var entryRef) &&
					entryRef.Value.State == ActivationState.Pending)
				{
					entryRef.Value.State = ActivationState.Cancelled;
				}

				commands.Despawn(requestEntity);
			}
		);
	}

	private static void EnsureResources(World world)
	{
		world.GetOrCreateResource<ActivationConfig>();
		world.GetOrCreateResource<ActivationRuntimeState>();
		world.GetOrCreateResource<ActivationEventsResource>();
		world.GetOrCreateResource<ActivationDispatchQueueResource>();
	}

}

[Query]
[With<ActivationEntry>]
internal readonly partial struct ActivationEntryQuery;

[Query]
[With<ActivationCancellationRequest>]
internal readonly partial struct ActivationCancellationQuery;
