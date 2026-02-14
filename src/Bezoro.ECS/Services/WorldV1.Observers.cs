using Bezoro.ECS.Internal;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Services;

public sealed partial class WorldV1
{
	private readonly object                                        _observerGate        = new();
	private readonly Dictionary<int, List<WeakObserver>>           _onRemoveInObservers = new();
	private readonly Dictionary<int, List<WeakObserver>>           _onAddObservers      = new();
	private readonly Dictionary<int, List<WeakObserver>>           _onAddRefObservers   = new();
	private readonly Dictionary<int, List<WeakObserver>>           _onSetRefObservers   = new();

	public IDisposable Observe<T>(Action<Entity, T> observer) where T : struct
	{
		ThrowIfDisposed();
		if (observer is null) throw new ArgumentNullException(nameof(observer));

		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		var weakObserver = new WeakObserver(observer);
		AddObserver(_onAddObservers, typeId, weakObserver);
		return new ObserverSubscription(() => RemoveObserver(_onAddObservers, typeId, weakObserver), observer);
	}

	public IDisposable ObserveAdd<T>(OnAddObserver<T> observer) where T : struct
	{
		ThrowIfDisposed();
		if (observer is null) throw new ArgumentNullException(nameof(observer));

		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		var weakObserver = new WeakObserver(observer);
		AddObserver(_onAddRefObservers, typeId, weakObserver);
		return new ObserverSubscription(() => RemoveObserver(_onAddRefObservers, typeId, weakObserver), observer);
	}

	public IDisposable ObserveRemove<T>(OnRemoveObserver<T> observer) where T : struct
	{
		ThrowIfDisposed();
		if (observer is null) throw new ArgumentNullException(nameof(observer));

		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		var wrapped = (Action<Entity, object>)((entity, value) =>
												  {
													  var typed = (T)value;
													  observer(entity, in typed);
												  });

		var weakObserver = new WeakObserver(wrapped);
		AddObserver(_onRemoveInObservers, typeId, weakObserver);
		return new ObserverSubscription(() => RemoveObserver(_onRemoveInObservers, typeId, weakObserver), wrapped);
	}

	public IDisposable ObserveSet<T>(OnSetObserver<T> observer) where T : struct
	{
		ThrowIfDisposed();
		if (observer is null) throw new ArgumentNullException(nameof(observer));

		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		var weakObserver = new WeakObserver(observer);
		AddObserver(_onSetRefObservers, typeId, weakObserver);
		return new ObserverSubscription(() => RemoveObserver(_onSetRefObservers, typeId, weakObserver), observer);
	}

	private List<(int TypeId, object Value)> CaptureRemovedComponents(Entity entity)
	{
		var location = _entityManager.GetLocation(entity);
		if (!location.IsValid)
			return [];

		var archetype = _archetypes[location.ArchetypeId];
		DecomposeLocation(archetype, location, out int chunkIndex, out int slotIndex);
		var chunk   = archetype.Chunks[chunkIndex];
		var removed = new List<(int TypeId, object Value)>(archetype.TypeIds.Length);
		for (var i = 0; i < archetype.TypeIds.Length; i++)
		{
			int typeId = archetype.TypeIds[i];
			if (ComponentTypeRegistry.IsRelationship(typeId))
				continue;

			removed.Add((typeId, chunk.GetValue(i, slotIndex)));
		}

		return removed;
	}

	private void RaiseOnAdd<T>(Entity entity, int typeId) where T : struct
	{
		if (!IsPlayingBackCommands)
			return;

		var valueHandlers = SnapshotObservers<Action<Entity, T>>(_onAddObservers, typeId);
		var refHandlers   = SnapshotObservers<OnAddObserver<T>>(_onAddRefObservers, typeId);
		if ((valueHandlers?.Count ?? 0) == 0 && (refHandlers?.Count ?? 0) == 0)
			return;

		if (!TryGetComponentArray(entity, typeId, out var chunk, out int slot, out int componentIndex))
			return;

		ref var component = ref chunk.GetReference<T>(componentIndex, slot);

		if (refHandlers is not null)
			for (var i = 0; i < refHandlers.Count; i++)
				refHandlers[i](entity, ref component);

		if (valueHandlers is null) return;

		for (var i = 0; i < valueHandlers.Count; i++)
			valueHandlers[i](entity, component);
	}

	private void RaiseOnRemove(Entity entity, int typeId, object removedValue)
	{
		if (!IsPlayingBackCommands)
			return;

		var handlers = SnapshotObservers<Action<Entity, object>>(_onRemoveInObservers, typeId);
		if (handlers is null)
			return;

		for (var i = 0; i < handlers.Count; i++)
			handlers[i](entity, removedValue);
	}

	private void RaiseOnSet<T>(Entity entity, int typeId) where T : struct
	{
		if (!IsPlayingBackCommands)
			return;

		var handlers = SnapshotObservers<OnSetObserver<T>>(_onSetRefObservers, typeId);
		if (handlers is null || handlers.Count == 0)
			return;

		if (!TryGetComponentArray(entity, typeId, out var chunk, out int slot, out int componentIndex))
			return;

		ref var component = ref chunk.GetReference<T>(componentIndex, slot);
		for (var i = 0; i < handlers.Count; i++)
			handlers[i](entity, ref component);
	}

	private void AddObserver(
		Dictionary<int, List<WeakObserver>> observers,
		int                                 typeId,
		WeakObserver                        observer)
	{
		lock (_observerGate)
		{
			if (!observers.TryGetValue(typeId, out var handlers))
			{
				handlers          = [];
				observers[typeId] = handlers;
			}

			handlers.Add(observer);
		}
	}

	private void RemoveObserver(
		Dictionary<int, List<WeakObserver>> observers,
		int                                 typeId,
		WeakObserver                        observer)
	{
		lock (_observerGate)
		{
			if (!observers.TryGetValue(typeId, out var handlers))
				return;

			handlers.Remove(observer);
			if (handlers.Count == 0)
				observers.Remove(typeId);
		}
	}

	private List<TObserver>? SnapshotObservers<TObserver>(Dictionary<int, List<WeakObserver>> observers, int typeId)
		where TObserver : class
	{
		lock (_observerGate)
		{
			if (!observers.TryGetValue(typeId, out var handlers) || handlers.Count == 0)
				return null;

			var liveObservers = new List<TObserver>(handlers.Count);
			var writeIndex    = 0;
			for (var i = 0; i < handlers.Count; i++)
			{
				if (!handlers[i].TryGet(out TObserver? observer))
					continue;

				if (writeIndex != i)
					handlers[writeIndex] = handlers[i];

				writeIndex++;
				if (observer is null)
					continue;

				liveObservers.Add(observer);
			}

			if (writeIndex == 0)
			{
				observers.Remove(typeId);
				return null;
			}

			if (writeIndex < handlers.Count)
				handlers.RemoveRange(writeIndex, handlers.Count - writeIndex);

			return liveObservers;
		}
	}

	private void ClearObservers()
	{
		lock (_observerGate)
		{
			_onAddObservers.Clear();
			_onAddRefObservers.Clear();
			_onSetRefObservers.Clear();
			_onRemoveInObservers.Clear();
		}
	}

	private sealed class WeakObserver(Delegate observer)
	{
		private readonly WeakReference<Delegate> _reference = new(observer ?? throw new ArgumentNullException(nameof(observer)));

		public bool TryGet<TObserver>(out TObserver? observer) where TObserver : class
		{
			if (_reference.TryGetTarget(out var target) && target is TObserver typedObserver)
			{
				observer = typedObserver;
				return true;
			}

			observer = null;
			return false;
		}
	}

	private sealed class ObserverSubscription(Action unsubscribe, object keepAlive) : IDisposable
	{
		private Action? _unsubscribe = unsubscribe;
		private object? _keepAlive = keepAlive;

		~ObserverSubscription()
		{
			Dispose(false);
		}

		public void Dispose() =>
			Dispose(true);

		private void Dispose(bool disposing)
		{
			var unsubscribe = Interlocked.Exchange(ref _unsubscribe, null);
			_keepAlive = null;
			unsubscribe?.Invoke();
			if (disposing)
				GC.SuppressFinalize(this);
		}
	}
}
