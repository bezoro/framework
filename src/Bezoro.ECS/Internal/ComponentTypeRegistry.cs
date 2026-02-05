using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Internal;

internal static class ComponentTypeRegistry
{
	private static readonly object Sync = new();
	private static readonly Dictionary<Type, int> TypeToId = new();
	private static readonly List<Type> IdToType = [];

	public static int Count
	{
		get
		{
			lock (Sync)
			{
				return IdToType.Count;
			}
		}
	}

	public static int GetOrCreate<T>() where T : struct, IComponent =>
		GetOrCreate(typeof(T));

	public static int GetOrCreate(Type type)
	{
		if (type is null) throw new ArgumentNullException(nameof(type));

		if (!type.IsValueType || !typeof(IComponent).IsAssignableFrom(type))
			throw new ArgumentException("Component types must be structs implementing IComponent.", nameof(type));

		lock (Sync)
		{
			if (TypeToId.TryGetValue(type, out int id))
				return id;

			id = IdToType.Count;
			TypeToId[type] = id;
			IdToType.Add(type);
			return id;
		}
	}

	public static Type GetType(int id)
	{
		lock (Sync)
		{
			return IdToType[id];
		}
	}
}
