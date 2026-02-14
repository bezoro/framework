using System;
using System.Collections.Generic;
using System.Numerics;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.MovementSystem.Types;
using Bezoro.GameSystems.StreamingSystem.Types;

namespace Bezoro.GameSystems.StreamingSystem.Services;

/// <summary>
///     ECS system that applies distance-based streaming transitions.
/// </summary>
[Reads<Position>]
[Writes<StreamState>]
public sealed class StreamingSystem : ISystem
{
	private const float DefaultStreamInDistance  = 100f;
	private const float DefaultStreamOutDistance = 120f;
	private const int   DefaultMaxEntitiesPerTick = 50;
	private QueryHandle<StreamingQuerySpec> _streamingQuery;

	/// <summary>
	///     Raised when an entity changes streaming state.
	/// </summary>
	public event Action<StreamingStateChangedEvent>? Changed;

	/// <summary>
	///     Raised when an entity transitions to streamed-in.
	/// </summary>
	public event Action<StreamingStateChangedEvent>? StreamedIn;

	/// <summary>
	///     Raised when an entity transitions to streamed-out.
	/// </summary>
	public event Action<StreamingStateChangedEvent>? StreamedOut;

	public Stage Stage => Stage.Tick;

	public SystemLoopPhase LoopPhase => SystemLoopPhase.Tick;

	public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryTick;

	/// <inheritdoc />
	public void OnCreate(World world)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));
		EnsureResources(world);
		_streamingQuery = world.Compile<StreamingQuerySpec>();
	}

	/// <inheritdoc />
	public void Update(in SystemContext context)
	{
		var world = context.World;
		if (world is null) throw new ArgumentNullException(nameof(world));
		EnsureResources(world);

		ref var config = ref world.GetResource<StreamingConfig>();
		ValidateConfig(in config);

		int total = CountStreamables(world, _streamingQuery);
		if (total == 0)
		{
			ref var emptyRuntime = ref world.GetResource<StreamingRuntimeState>();
			emptyRuntime.NextEntityIndex = 0;
			return;
		}

		int maxEntitiesPerTick = config.MaxEntitiesPerTick <= 0
									 ? total
									 : Math.Min(config.MaxEntitiesPerTick, total);

		ref var runtime = ref world.GetResource<StreamingRuntimeState>();
		int startIndex = NormalizeStartIndex(runtime.NextEntityIndex, total);
		int selectionEndExclusive = startIndex + maxEntitiesPerTick;
		var wraps = selectionEndExclusive > total;
		if (wraps)
			selectionEndExclusive -= total;

		var inDistanceSquared  = config.StreamInDistance * config.StreamInDistance;
		var outDistanceSquared = config.StreamOutDistance * config.StreamOutDistance;
		var referencePosition  = config.ReferencePosition;

		ApplyStreamingTransitions(
			world,
			_streamingQuery,
			referencePosition,
			inDistanceSquared,
			outDistanceSquared,
			startIndex,
			wraps,
			selectionEndExclusive
		);

		runtime.NextEntityIndex = startIndex + maxEntitiesPerTick;
		if (runtime.NextEntityIndex >= total)
			runtime.NextEntityIndex -= total;
	}

	private void ApplyStreamingTransitions(
		World   world,
		QueryHandle<StreamingQuerySpec> queryHandle,
		Vector3  referencePosition,
		float    inDistanceSquared,
		float    outDistanceSquared,
		int      startIndex,
		bool     wraps,
		int      endExclusive)
	{
		using var cursor = world.Execute(queryHandle);
		if (!cursor.MoveNext())
			return;

		var entities = cursor.Current;
		for (var i = 0; i < entities.Length; i++)
		{
			if (!IsSelectedIndex(i, startIndex, wraps, endExclusive))
			{
				continue;
			}

			var position = cursor.Get<Position>(i);
			ref var state = ref cursor.Get<StreamState>(i);
			float distanceSquared = GetDistanceSquared(referencePosition, in position);

			StreamingTransition? transition = TryTransition(
				ref state,
				distanceSquared,
				inDistanceSquared,
				outDistanceSquared
			);

			if (transition.HasValue)
				Publish(world, entities[i], transition.Value, distanceSquared);
		}
	}

	private void Publish(
		World               world,
		Entity               targetEntity,
		StreamingTransition  transition,
		float                distanceSquared)
	{
		var eventData = new StreamingStateChangedEvent(targetEntity, transition, distanceSquared);
		ref var events = ref world.GetResource<StreamingEventsResource>();
		events.Enqueue(in eventData);

		try
		{
			Changed?.Invoke(eventData);
			GetHandler(transition)?.Invoke(eventData);
		}
		catch
		{
			// Event handler exceptions should not break simulation.
		}
	}

	private Action<StreamingStateChangedEvent>? GetHandler(StreamingTransition transition) =>
		transition switch
		{
			StreamingTransition.StreamedIn => StreamedIn,
			StreamingTransition.StreamedOut => StreamedOut,
			_ => null
		};

	private static bool IsSelectedIndex(int index, int startIndex, bool wraps, int endExclusive)
	{
		if (!wraps)
			return index >= startIndex && index < endExclusive;

		return index >= startIndex || index < endExclusive;
	}

	private static float GetDistanceSquared(Vector3 referencePosition, in Position entityPosition)
	{
		var dx = referencePosition.X - entityPosition.X;
		var dy = referencePosition.Y - entityPosition.Y;
		var dz = referencePosition.Z - entityPosition.Z;
		return dx * dx + dy * dy + dz * dz;
	}

	private static StreamingTransition? TryTransition(
		ref StreamState streamState,
		float          distanceSquared,
		float          inDistanceSquared,
		float          outDistanceSquared)
	{
		if (!streamState.IsStreamedIn && distanceSquared <= inDistanceSquared)
		{
			streamState.IsStreamedIn = true;
			return StreamingTransition.StreamedIn;
		}

		if (streamState.IsStreamedIn && distanceSquared > outDistanceSquared)
		{
			streamState.IsStreamedIn = false;
			return StreamingTransition.StreamedOut;
		}

		return null;
	}

	private static int NormalizeStartIndex(int startIndex, int total)
	{
		if (startIndex < 0 || startIndex >= total)
			return 0;

		return startIndex;
	}

	private static int CountStreamables(World world, QueryHandle<StreamingQuerySpec> queryHandle)
	{
		using var cursor = world.Execute(queryHandle);
		if (!cursor.MoveNext())
			return 0;

		return cursor.Current.Length;
	}

	private static void ValidateConfig(in StreamingConfig config)
	{
		if (config.StreamInDistance < 0f)
			throw new ArgumentOutOfRangeException(
				nameof(config), "StreamInDistance must be non-negative."
			);

		if (config.StreamOutDistance < config.StreamInDistance)
			throw new ArgumentException(
				$"StreamOutDistance ({config.StreamOutDistance}) must be >= StreamInDistance ({config.StreamInDistance}) to prevent flickering.",
				nameof(config)
			);
	}

	private static void EnsureResources(World world)
	{
		try
		{
			_ = world.GetResource<StreamingConfig>();
		}
		catch (KeyNotFoundException)
		{
			world.SetResource(
				new StreamingConfig
				{
					ReferencePosition    = Vector3.Zero,
					StreamInDistance     = DefaultStreamInDistance,
					StreamOutDistance    = DefaultStreamOutDistance,
					MaxEntitiesPerTick = DefaultMaxEntitiesPerTick
				}
			);
		}

		try
		{
			_ = world.GetResource<StreamingRuntimeState>();
		}
		catch (KeyNotFoundException)
		{
			world.SetResource(new StreamingRuntimeState());
		}

		try
		{
			_ = world.GetResource<StreamingEventsResource>();
		}
		catch (KeyNotFoundException)
		{
			world.SetResource(new StreamingEventsResource());
		}
	}

	private readonly struct StreamingQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.All<StreamState>();
		}
	}
}
