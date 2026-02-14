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
	private QueryHandle<ActivationEntryQuerySpec> _entryQuery;
	private QueryHandle<ActivationCancellationQuerySpec> _cancellationQuery;

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
		_entryQuery = world.Compile<ActivationEntryQuerySpec>();
		_cancellationQuery = world.Compile<ActivationCancellationQuerySpec>();
	}

	/// <inheritdoc />
	public void Update(in SystemContext context)
	{
		var world = context.World;
		if (world is null) throw new ArgumentNullException(nameof(world));
		EnsureResources(world);

		var entriesByHandle = BuildEntryIndex(world, _entryQuery);
		ApplyCancellations(world, context.Commands, entriesByHandle, _cancellationQuery);

		var pendingEntries = CollectPendingEntries(world, _entryQuery, out var activatedCount);
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

	private void PublishCompletionIfNeeded(World world, ActivationRuntimeState runtime)
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
		World world,
		QueryHandle<ActivationEntryQuerySpec> queryHandle,
		out int activatedCount)
	{
		var pendingEntries = new List<PendingActivationCandidate>();
		activatedCount = 0;

		using var cursor = world.Execute(queryHandle);
		if (!cursor.MoveNext())
			return pendingEntries;

		var entities = cursor.Current;
		for (var i = 0; i < entities.Length; i++)
		{
			if (!world.TryGet(entities[i], out ActivationEntry entry))
				continue;

			if (entry.State == ActivationState.Activated)
			{
				activatedCount++;
				continue;
			}

			if (entry.State != ActivationState.Pending)
				continue;

			pendingEntries.Add(new(entities[i], entry.Priority, entry.Handle.Id));
		}

		return pendingEntries;
	}

	private static Dictionary<int, Entity> BuildEntryIndex(
		World world,
		QueryHandle<ActivationEntryQuerySpec> queryHandle)
	{
		var entriesByHandle = new Dictionary<int, Entity>();
		using var cursor = world.Execute(queryHandle);
		if (!cursor.MoveNext())
			return entriesByHandle;

		var entities = cursor.Current;
		for (var i = 0; i < entities.Length; i++)
		{
			if (!world.TryGet(entities[i], out ActivationEntry entry))
				continue;

			if (!entry.Handle.IsValid)
				continue;

			entriesByHandle[entry.Handle.Id] = entities[i];
		}

		return entriesByHandle;
	}

	private static void ApplyCancellations(
		World                  world,
		CommandStream          commands,
		IReadOnlyDictionary<int, Entity> entriesByHandle,
		QueryHandle<ActivationCancellationQuerySpec> queryHandle)
	{
		using var cursor = world.Execute(queryHandle);
		if (!cursor.MoveNext())
			return;

		var entities = cursor.Current;
		for (var i = 0; i < entities.Length; i++)
		{
			var requestEntity = entities[i];
			var request = cursor.Get<ActivationCancellationRequest>(i);

			if (entriesByHandle.TryGetValue(request.Handle.Id, out var entryEntity) &&
				world.TryGet(entryEntity, out ActivationEntry entry) &&
				entry.State == ActivationState.Pending)
			{
				entry.State = ActivationState.Cancelled;
				world.Set(entryEntity, in entry);
			}

			commands.Destroy(requestEntity);
		}
	}

	private static void EnsureResources(World world)
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

	private readonly struct ActivationEntryQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.All<ActivationEntry>();
	}

	private readonly struct ActivationCancellationQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.All<ActivationCancellationRequest>();
	}

}
