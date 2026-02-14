using System.Runtime.CompilerServices;

namespace Bezoro.ECS.Internal.Fixed;

internal sealed class ComponentPool<T>(int entityCapacity) : IComponentPool where T : struct
{
	private readonly bool[] _presentByEntity    = new bool[entityCapacity];
	private readonly int[]  _denseEntities      = new int[entityCapacity];
	private readonly int[]  _denseIndexByEntity = CreateDenseIndexByEntity(entityCapacity);
	private readonly T[]    _valuesByEntity     = new T[entityCapacity];

	public bool IsManagedLane { get; } = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

	public int Population { get; private set; }

	public bool Has(int entityId) => _presentByEntity[entityId];

	public bool TryGet(int entityId, out T value)
	{
		if (!_presentByEntity[entityId])
		{
			value = default;
			return false;
		}

		value = _valuesByEntity[entityId];
		return true;
	}

	public ReadOnlySpan<int> GetDenseEntities() => new(_denseEntities, 0, Population);

	public ref T GetRef(int entityId)
	{
		if (!_presentByEntity[entityId])
			throw new KeyNotFoundException(
				$"Entity '{entityId}' does not contain component '{typeof(T).Name}'."
			);

		return ref _valuesByEntity[entityId];
	}

	public void Clear()
	{
		if (IsManagedLane)
			Array.Clear(_valuesByEntity, 0, _valuesByEntity.Length);

		Array.Clear(_presentByEntity, 0, _presentByEntity.Length);
		Array.Fill(_denseIndexByEntity, -1);
		Population = 0;
	}

	public void Remove(int entityId)
	{
		if (!_presentByEntity[entityId])
			return;

		_presentByEntity[entityId] = false;
		if (IsManagedLane)
			_valuesByEntity[entityId] = default;

		int removedDenseIndex = _denseIndexByEntity[entityId];
		int lastDenseIndex    = Population - 1;
		if (removedDenseIndex != lastDenseIndex)
		{
			int movedEntityId = _denseEntities[lastDenseIndex];
			_denseEntities[removedDenseIndex]  = movedEntityId;
			_denseIndexByEntity[movedEntityId] = removedDenseIndex;
		}

		_denseEntities[lastDenseIndex] = 0;
		_denseIndexByEntity[entityId]  = -1;
		Population--;
	}

	public void Set(int entityId, in T value)
	{
		_valuesByEntity[entityId] = value;
		if (_presentByEntity[entityId])
			return;

		_presentByEntity[entityId]    = true;
		_denseIndexByEntity[entityId] = Population;
		_denseEntities[Population++]  = entityId;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal ref T GetRefUnchecked(int entityId) => ref _valuesByEntity[entityId];

	private static int[] CreateDenseIndexByEntity(int entityCapacity)
	{
		var denseIndex = new int[entityCapacity];
		Array.Fill(denseIndex, -1);
		return denseIndex;
	}
}
