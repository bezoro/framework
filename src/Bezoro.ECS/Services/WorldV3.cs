using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal.V3;
using Bezoro.ECS.Types;
using System.Runtime.CompilerServices;

namespace Bezoro.ECS.Services;

/// <summary>
/// High-performance ECS runtime with fixed-capacity storage and parallel wave system execution.
/// </summary>
public sealed class WorldV3 : IDisposable
{
	private readonly World                 _core;
	private readonly ParallelSystemSchedulerV3 _scheduler;
	private          bool                      _disposed;

	public WorldV3(WorldV3Config config)
	{
		if (config is null) throw new ArgumentNullException(nameof(config));
		config.Validate();

		_core = new(new()
		{
			EntityCapacity = config.EntityCapacity,
			ComponentTypeCapacity = config.ComponentTypeCapacity,
			CommandCapacity = config.CommandCapacity,
			CommandPayloadCapacityPerType = config.CommandPayloadCapacityPerType,
			QueryResultCapacity = config.QueryResultCapacity,
			ChunkCapacity = config.ChunkCapacity,
			OverflowPolicy = config.OverflowPolicy
		});
		_scheduler = new(this, _core, config.ParallelWorkerCount);
	}

	public int EntityCount
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _core.EntityCount;
	}

	public Entity CreateEntity()
	{
		ThrowIfDisposed();
		return _core.CreateEntityInternal();
	}

	public void Destroy(Entity entity)
	{
		ThrowIfDisposed();
		_core.DestroyEntityInternal(entity);
	}

	public void Set<T>(Entity entity, in T component) where T : struct
	{
		ThrowIfDisposed();
		_core.ApplySetFromCommand(entity, in component);
	}

	public void Remove<T>(Entity entity) where T : struct
	{
		ThrowIfDisposed();
		int typeId = _core.GetOrCreateComponentTypeId<T>();
		_core.RemoveComponentFromCommand(entity, typeId);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ref T Get<T>(Entity entity) where T : unmanaged
	{
		return ref _core.Get<T>(entity);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGet<T>(Entity entity, out T component) where T : unmanaged
	{
		return _core.TryGet(entity, out component);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetManaged<T>(Entity entity, out T component) where T : struct
	{
		return _core.TryGetManaged(entity, out component);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Has<T>(Entity entity) where T : struct
	{
		return _core.Has<T>(entity);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsAlive(Entity entity)
	{
		return _core.IsAlive(entity);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ComponentAccessor<T> GetAccessor<T>() where T : unmanaged
	{
		return _core.GetAccessor<T>();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public CommandStream CreateCommandStream()
	{
		return _core.CreateCommandStream();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Playback(CommandStream stream)
	{
		_core.Playback(stream);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public QueryHandle<TSpec> Compile<TSpec>() where TSpec : struct, ICompiledQuerySpec
	{
		return _core.Compile<TSpec>();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public QueryCursor Execute<TSpec>(QueryHandle<TSpec> handle) where TSpec : struct, ICompiledQuerySpec
	{
		return _core.Execute(handle);
	}

	public void Run<TSpec, TJob, T1>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1>
		where T1 : unmanaged
	{
		_core.RunDirectFast<TSpec, TJob, T1>(handle, job);
	}

	public void Run<TSpec, TJob, T1, T2>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2>
		where T1 : unmanaged
		where T2 : unmanaged
	{
		_core.RunDirectFast<TSpec, TJob, T1, T2>(handle, job);
	}

	public void Run<TSpec, TJob, T1, T2, T3>(QueryHandle<TSpec> handle, TJob job)
		where TSpec : struct, ICompiledQuerySpec
		where TJob : struct, IForEach<T1, T2, T3>
		where T1 : unmanaged
		where T2 : unmanaged
		where T3 : unmanaged
	{
		_core.RunDirectFast<TSpec, TJob, T1, T2, T3>(handle, job);
	}

	public void AddSystem(ISystemV3 system)
	{
		ThrowIfDisposed();
		_scheduler.AddSystem(system);
	}

	public void Tick(float deltaTime)
	{
		ThrowIfDisposed();
		_scheduler.Tick(deltaTime);
	}

	public SchedulerDiagnosticsV3 GetSchedulerDiagnostics()
	{
		ThrowIfDisposed();
		return _scheduler.GetDiagnostics();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public WorldDiagnostics GetDiagnostics()
	{
		return _core.GetDiagnostics();
	}

	public void Reset()
	{
		ThrowIfDisposed();
		_core.Reset();
		_scheduler.ResetCommandStreams();
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_scheduler.Dispose();
		_core.Dispose();
		_disposed = true;
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(WorldV3));
	}
}

