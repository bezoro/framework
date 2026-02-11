using Bezoro.ECS.Internal;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Services;

public sealed partial class World
{
	private readonly Dictionary<int, List<Action<Entity, object>>> _onRemoveInObservers = new();
	private readonly Dictionary<int, List<Delegate>>               _onAddObservers      = new();
	private readonly Dictionary<int, List<Delegate>>               _onAddRefObservers   = new();

	public IDisposable Observe<T>(Action<Entity, T> observer) where T : struct
	{
		ThrowIfDisposed();
		if (observer is null) throw new ArgumentNullException(nameof(observer));

		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (!_onAddObservers.TryGetValue(typeId, out var handlers))
		{
			handlers                = [];
			_onAddObservers[typeId] = handlers;
		}

		handlers.Add(observer);
		return new ObserverSubscription(() => RemoveObserver(_onAddObservers, typeId, observer));
	}

	public IDisposable ObserveAdd<T>(OnAddObserver<T> observer) where T : struct
	{
		ThrowIfDisposed();
		if (observer is null) throw new ArgumentNullException(nameof(observer));

		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (!_onAddRefObservers.TryGetValue(typeId, out var handlers))
		{
			handlers                   = [];
			_onAddRefObservers[typeId] = handlers;
		}

		handlers.Add(observer);
		return new ObserverSubscription(() => RemoveObserver(_onAddRefObservers, typeId, observer));
	}

	public IDisposable ObserveRemove<T>(OnRemoveObserver<T> observer) where T : struct
	{
		ThrowIfDisposed();
		if (observer is null) throw new ArgumentNullException(nameof(observer));

		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		if (!_onRemoveInObservers.TryGetValue(typeId, out var handlers))
		{
			handlers                     = [];
			_onRemoveInObservers[typeId] = handlers;
		}

		var wrapped = (Action<Entity, object>)((entity, value) =>
												  {
													  var typed = (T)value;
													  observer(entity, in typed);
												  });

		handlers.Add(wrapped);
		return new ObserverSubscription(() => RemoveObserver(_onRemoveInObservers, typeId, wrapped));
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

		bool hasValueHandlers = _onAddObservers.TryGetValue(typeId, out var valueHandlers);
		bool hasRefHandlers   = _onAddRefObservers.TryGetValue(typeId, out var refHandlers);
		if (!hasValueHandlers && !hasRefHandlers)
			return;

		if (!TryGetComponentArray(entity, typeId, out var chunk, out int slot, out int componentIndex))
			return;

		ref var component = ref chunk.GetReference<T>(componentIndex, slot);

		if (hasRefHandlers)
			for (var i = 0; i < refHandlers!.Count; i++)
			{
				if (refHandlers[i] is OnAddObserver<T> observer)
					observer(entity, ref component);
			}

		if (!hasValueHandlers) return;

		{
			for (var i = 0; i < valueHandlers!.Count; i++)
			{
				if (valueHandlers[i] is Action<Entity, T> observer)
					observer(entity, component);
			}
		}
	}

	private void RaiseOnRemove(Entity entity, int typeId, object removedValue)
	{
		if (!IsPlayingBackCommands)
			return;

		if (_onRemoveInObservers.TryGetValue(typeId, out var inHandlers))
			for (var i = 0; i < inHandlers.Count; i++)
				inHandlers[i](entity, removedValue);
	}

	private static void RemoveObserver<TObserver>(
		Dictionary<int, List<TObserver>> observers,
		int                              typeId,
		TObserver                        observer)
		where TObserver : class
	{
		if (!observers.TryGetValue(typeId, out var handlers))
			return;

		handlers.Remove(observer);
		if (handlers.Count == 0)
			observers.Remove(typeId);
	}

	private sealed class ObserverSubscription(Action unsubscribe) : IDisposable
	{
		private Action? _unsubscribe = unsubscribe;

		public void Dispose()
		{
			var unsubscribe = Interlocked.Exchange(ref _unsubscribe, null);
			unsubscribe?.Invoke();
		}
	}
}
