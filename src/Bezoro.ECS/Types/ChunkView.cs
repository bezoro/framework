using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal;

namespace Bezoro.ECS.Types;

/// <summary>
///     Provides access to a chunk of entities and their component arrays.
/// </summary>
public readonly struct ChunkView
{
	private readonly Array[]  _components;
	private readonly Entity[] _entities;
	private readonly int[]    _typeIndexById;

	internal ChunkView(Entity[] entities, Array[] components, int count, int[] typeIndexById)
	{
		_entities      = entities;
		_components    = components;
		Count          = count;
		_typeIndexById = typeIndexById;
	}

	/// <summary>
	///     Gets the number of entities in this chunk.
	/// </summary>
	public int Count { get; }

	/// <summary>
	///     Gets the entities in this chunk.
	/// </summary>
	public ReadOnlySpan<Entity> Entities => new(_entities, 0, Count);

	/// <summary>
	///     Attempts to get the component span for type <typeparamref name="T" />.
	/// </summary>
	/// <typeparam name="T">The component type.</typeparam>
	/// <param name="components">The component span if present.</param>
	/// <returns><c>true</c> if the component exists in the chunk; otherwise, <c>false</c>.</returns>
	public bool TryComponents<T>(out Span<T> components) where T : struct, IComponent
	{
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		int index  = GetIndex(typeId);
		if (index < 0)
		{
			components = default;
			return false;
		}

		components = new((T[])_components[index], 0, Count);
		return true;
	}

	/// <summary>
	///     Gets the component span for type <typeparamref name="T" />.
	/// </summary>
	/// <typeparam name="T">The component type.</typeparam>
	/// <returns>The component span for the chunk.</returns>
	/// <exception cref="KeyNotFoundException">Thrown when the component is not present in the chunk.</exception>
	public Span<T> Components<T>() where T : struct, IComponent
	{
		int typeId = ComponentTypeRegistry.GetOrCreate<T>();
		int index  = GetIndex(typeId);
		if (index < 0)
			throw new KeyNotFoundException($"Component of type {typeof(T).Name} not found in chunk.");

		return new((T[])_components[index], 0, Count);
	}

	private int GetIndex(int typeId)
	{
		if (typeId < 0 || typeId >= _typeIndexById.Length) return -1;

		return _typeIndexById[typeId];
	}
}
